using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using EnarcLabs.Phoneme.Binding.Security;
using Image = System.Drawing.Image;

namespace EnarcLabs.Phoneme.Binding
{
    public delegate void PeerJoinLeaveHandler(PhonemePeer peer);

    public class PhonemeClient : IDisposable
    {
        internal Guid SymetricKey = Guid.Empty;
        private readonly UdpClient _udpClient;
        private readonly TcpListener _tcpListener;

        internal byte[] PublicKey { get; }

        internal byte[] PrivateKey { get; }
                
        /// <summary>
        /// An optional image used for this user during communication.
        /// </summary>
        public byte[] ProfilePicture { get; }

        /// <summary>
        /// An optional name displayed to other clients during communication.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// The port used to transmit and recieve messages.
        /// </summary>
        public int NetworkPort { get; }

        /// <summary>
        /// Used to allow or deny entry into the mesh network.
        /// </summary>
        public ITrustVerifier TrustVerifier { get; }

        private readonly HashSet<PhonemePeer> _knownPeersSet;
        public ObservableCollection<PhonemePeer> KnownPeers { get; }

        public PhonemeClient(int networkPort, byte[] publicKey, byte[] privateKey, ITrustVerifier trustVerifier, string displayName = null, byte[] profilePicture = null)
        {
            if(publicKey == null)
                throw new ArgumentNullException(nameof(publicKey));
            if(privateKey == null)
                throw new ArgumentNullException(nameof(privateKey));
            if(trustVerifier == null)
                throw new ArgumentNullException(nameof(trustVerifier));

            _udpClient = new UdpClient(networkPort, AddressFamily.InterNetwork)
            {
                EnableBroadcast = true,
                MulticastLoopback = true,
            };
            _tcpListener = new TcpListener(IPAddress.Any, networkPort);
            _knownPeersSet = new HashSet<PhonemePeer>();
            KnownPeers = new ObservableCollection<PhonemePeer>();
            NetworkPort = networkPort;
            PublicKey = publicKey;
            PrivateKey = privateKey;
            DisplayName = displayName;
            TrustVerifier = trustVerifier;

            if (profilePicture == null) return;

            using (var img = Image.FromStream(new MemoryStream(profilePicture)))
            using (var output = new MemoryStream())
            {
                if (img.Size.Width > 256 || img.Size.Height > 256)
                    throw new ArgumentException("Profile pictures can only be 256x256 or smaller.");

                img.Save(output, ImageFormat.Jpeg);
                ProfilePicture = output.ToArray();
            }
        }

        public void Start()
        {
            _udpClient.BeginReceive(UdpRecieveLoop, null);
            _tcpListener.Start();
            _tcpListener.BeginAcceptSocket(TcpAcceptLoop, null);

            using (var mem = new MemoryStream())
            {
                var wrt = new BinaryWriter(mem);
                wrt.Write(true); //New client added
                wrt.Write(PublicKey.Length);
                wrt.Write(PublicKey);
                //This is not necessary
                //Length is a fixed 16 bytes
                //wrt.Write(_broadcastGuid.ToByteArray());

                mem.Position = 0;
                GlobalHelpers.BroadcastPacket(mem.ToArray(), NetworkPort);
            }
        }

        private void TcpAcceptLoop(IAsyncResult result)
        {
            try
            {
                _tcpListener.BeginAcceptSocket(TcpAcceptLoop, null);
            }
            catch
            {
                //Called when disposed
                return;
            }

            using (var socket = _tcpListener.EndAcceptSocket(result))
            using (var stream = new NetworkStream(socket))
            {
                if (!(socket.RemoteEndPoint is IPEndPoint))
                    return;

                var rdr = new BinaryReader(stream);
                var wrt = new BinaryWriter(stream);
                switch ((PeerCommand)rdr.ReadByte())
                {
                    case PeerCommand.Identify:
                    {
                        byte[] pub;
                        if(!GlobalHelpers.VerifyIdentity(stream, out pub))
                            return;

                        var symGuid = rdr.ReadBytes(rdr.ReadInt32());
                        using (var rsa = OpenSslKey.DecodeRsaPrivateKey(PrivateKey))
                            SymetricKey = new Guid(rsa.Decrypt(symGuid, false));

                        var peer = new PhonemePeer(this, pub, new IPEndPoint(((IPEndPoint)socket.RemoteEndPoint).Address, NetworkPort));
                        if (!_knownPeersSet.Contains(peer))
                        {
                            _knownPeersSet.Add(peer);
                            //TODO: Remove this dependency
                            Application.Current.Dispatcher.Invoke(() => KnownPeers.Add(peer));
                            PeerJoin?.Invoke(peer);
                        }else
                            peer = _knownPeersSet.First(x => x.Equals(peer));

                        peer.DisplayName = rdr.ReadBoolean() ? rdr.ReadString() : null;
                        if (rdr.ReadBoolean())
                            rdr.ReadBytes(rdr.ReadInt32());

                        wrt.Write(DisplayName != null);
                        if(DisplayName != null)
                            wrt.Write(DisplayName);
                        wrt.Write(ProfilePicture != null);
                        if (ProfilePicture != null)
                        {
                            wrt.Write(ProfilePicture.Length);
                            wrt.Write(ProfilePicture);
                        }


                        break;
                    }
                    case PeerCommand.BinaryBlob:
                    {
                        byte[] pub;
                        if (!GlobalHelpers.VerifyIdentity(stream, out pub))
                            return;

                        var peer = new PhonemePeer(this, pub, new IPEndPoint(((IPEndPoint) socket.RemoteEndPoint).Address, NetworkPort));
                        if (!_knownPeersSet.Contains(peer))
                            return;
                        peer = _knownPeersSet.First(x => x.Equals(peer));

                        var blob = rdr.ReadBytes(rdr.ReadInt32());
                        GlobalHelpers.OneTimePad(blob, SymetricKey);

                        peer.OnMessageRecieved(blob);

                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void UdpRecieveLoop(IAsyncResult result)
        {
            try
            {
                _udpClient.BeginReceive(UdpRecieveLoop, null);
            }
            catch
            {
                //On disposed
                return;
            }

            IPEndPoint remote = null;
            var data = _udpClient.EndReceive(result, ref remote);

            if (remote == null || data == null || data.Length == 0)
                return;

            using (var mem = new MemoryStream(data))
            {
                var bin = new BinaryReader(mem);
                var add = bin.ReadBoolean();
                var pubKey = bin.ReadBytes(bin.ReadInt32());

                if(pubKey.Compare(PublicKey) || !TrustVerifier.VerifyTrust(pubKey))
                    return;

                var peer = new PhonemePeer(this, pubKey, new IPEndPoint(remote.Address, NetworkPort));
                if (add)
                {
                    if (!_knownPeersSet.Contains(peer))
                    {
                        _knownPeersSet.Add(peer);
                        Application.Current.Dispatcher.Invoke(() => KnownPeers.Add(peer));
                        PeerJoin?.Invoke(peer);
                    }
                    peer.PerformTcpHandshake();
                }
                else
                {
                    if (!_knownPeersSet.Remove(peer)) return;

                    var realPeer = KnownPeers.First(x => x.Equals(peer));
                    Application.Current.Dispatcher.Invoke(() => KnownPeers.Remove(realPeer));
                    PeerLeave?.Invoke(realPeer);
                }
            }

        }
        
        public void Dispose()
        {
            try
            {
                using (var mem = new MemoryStream())
                {
                    var wrt = new BinaryWriter(mem);
                    wrt.Write(false); //Client is leaving.
                    wrt.Write(PublicKey.Length);
                    wrt.Write(PublicKey);

                    mem.Position = 0;
                    GlobalHelpers.BroadcastPacket(mem.ToArray(), NetworkPort);
                }
            }
            catch
            {
                //Ignore this, we're dying anyway.
            }

            _udpClient?.Close();
            _tcpListener?.Stop();
        }

        public event PeerJoinLeaveHandler PeerJoin;
        public event PeerJoinLeaveHandler PeerLeave;
    }

    internal enum BroadcastCommand
    {
        PeerAnnounce = 0,
        PeerLeave = 1,
    }
}
