using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Windows;
using EnarcLabs.Phoneme.Binding.Security;

namespace EnarcLabs.Phoneme.Binding
{
    public delegate void PeerJoinHandler(PhonemePeer peer);

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

        private readonly HashSet<PhonemePeer> _knownPeersSet;
        public ObservableCollection<PhonemePeer> KnownPeers { get; }

        public PhonemeClient(int networkPort, byte[] publicKey, byte[] privateKey, string displayName = null, byte[] profilePicture = null)
        {
            if(publicKey == null)
                throw new ArgumentNullException(nameof(publicKey));
            if(privateKey == null)
                throw new ArgumentNullException(nameof(privateKey));

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
                wrt.Write(PublicKey.Length);
                wrt.Write(PublicKey);
                //This is not necessary
                //Length is a fixed 16 bytes
                //wrt.Write(_broadcastGuid.ToByteArray());

                mem.Position = 0;
                //Broadcast on every interface, not just the default one.
                foreach (var multicastAddress in NetworkInterface.GetAllNetworkInterfaces()
                    .Where(x => x.OperationalStatus == OperationalStatus.Up && x.SupportsMulticast &&
                                x.GetIPProperties().GetIPv4Properties() != null &&
                                NetworkInterface.LoopbackInterfaceIndex !=
                                x.GetIPProperties().GetIPv4Properties().Index)
                    .SelectMany(x => x.GetIPProperties().UnicastAddresses).Select(x => x.Address)
                    .Where(x => x.AddressFamily == AddressFamily.InterNetwork))
                {
                    using (var bClient = new UdpClient(new IPEndPoint(multicastAddress, 0)))
                    {
                        bClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                        bClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontRoute, 1);

                        _udpClient.Send(mem.ToArray(), (int)mem.Length,
                            new IPEndPoint(IPAddress.Broadcast, NetworkPort));
                    }
                }
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
                        var sigGuid = Guid.NewGuid();
                        //Length is fixed - 16 bytes
                        wrt.Write(sigGuid.ToByteArray());

                        var pub = rdr.ReadBytes(rdr.ReadInt32());
                        var sig = rdr.ReadBytes(rdr.ReadInt32());
                        using (var rsa = OpenSslKey.DecodeX509PublicKey(pub))
                        {
                            if(!rsa.VerifyData(sigGuid.ToByteArray(), new SHA256CryptoServiceProvider(), sig))
                                return;
                        }

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
                        var sigGuid = Guid.NewGuid();
                        //Length is fixed - 16 bytes
                        wrt.Write(sigGuid.ToByteArray());

                        var pub = rdr.ReadBytes(rdr.ReadInt32());
                        var sig = rdr.ReadBytes(rdr.ReadInt32());
                        using (var rsa = OpenSslKey.DecodeX509PublicKey(pub))
                        {
                            if (!rsa.VerifyData(sigGuid.ToByteArray(), new SHA256CryptoServiceProvider(), sig))
                                return;
                        }

                        var peer = new PhonemePeer(this, pub, new IPEndPoint(((IPEndPoint) socket.RemoteEndPoint).Address, NetworkPort));
                        if (!_knownPeersSet.Contains(peer))
                            return;
                        peer = _knownPeersSet.First(x => x.Equals(peer));

                        var blob = rdr.ReadBytes(rdr.ReadInt32());
                        unsafe
                        {
                            fixed (byte* key = SymetricKey.ToByteArray())
                            fixed (byte* ptr = blob)
                                for (var i = 0; i < blob.Length; i++)
                                    ptr[i] ^= key[i % 16];
                        }

                        peer.OnMessageRecieved(blob);

                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static byte[] ReadAndVerifyPublicKey(Stream stream)
        {
            var bin = new BinaryReader(stream);

            var sig = bin.ReadBytes(bin.ReadInt32());
            var pubKey = bin.ReadBytes(bin.ReadInt32());

            using (var rsa = OpenSslKey.DecodeX509PublicKey(pubKey))
                return !rsa.VerifyData(pubKey, new SHA256CryptoServiceProvider(), sig) ? null : pubKey;
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
                var pubKey = bin.ReadBytes(bin.ReadInt32());

                //if(pubKey.Compare(PublicKey))
                //    return;

                var peer = new PhonemePeer(this, pubKey, remote);
                if (!_knownPeersSet.Contains(peer))
                {
                    _knownPeersSet.Add(peer);
                    Application.Current.Dispatcher.Invoke(() => KnownPeers.Add(peer));
                    PeerJoin?.Invoke(peer);
                }
                peer.PerformTcpHandshake();
            }

        }
        
        public void Dispose()
        {
            _udpClient?.Close();
            _tcpListener?.Stop();
        }

        public event PeerJoinHandler PeerJoin;
    }
}
