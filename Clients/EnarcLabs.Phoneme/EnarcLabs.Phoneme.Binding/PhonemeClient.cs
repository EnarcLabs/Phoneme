using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using EnarcLabs.Phoneme.Binding.Security;

namespace EnarcLabs.Phoneme.Binding
{
    public class PhonemeClient : IDisposable
    {
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
                MulticastLoopback = false,
            };
            _tcpListener = new TcpListener(IPAddress.Any, networkPort);
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

            using (var rsa = OpenSslKey.DecodeRsaPrivateKey(PrivateKey))
            {
                var sig = rsa.SignData(PublicKey, new SHA256CryptoServiceProvider());

                using (var mem = new MemoryStream())
                {
                    var bin = new BinaryWriter(mem);
                    bin.Write(sig.Length);
                    bin.Write(sig);
                    bin.Write(PublicKey.Length);
                    bin.Write(PublicKey);

                    mem.Position = 0;
                    _udpClient.Send(mem.ToArray(), (int)mem.Length, new IPEndPoint(IPAddress.Broadcast, NetworkPort));
                }
            }
        }

        private void TcpAcceptLoop(IAsyncResult result)
        {
            _tcpListener.BeginAcceptSocket(TcpAcceptLoop, null);

            using (var socket = _tcpListener.EndAcceptSocket(result))
            using (var stream = new NetworkStream(socket))
            {
                var bin = new BinaryReader(stream);
                switch ((PeerCommand)bin.ReadByte())
                {
                    case PeerCommand.Identify:
                    {
                        var pub = ReadAndVerifyPublicKey(stream);
                        if(pub == null)
                            return;
                        var peer = new PhonemePeer(this, pub, socket.RemoteEndPoint as IPEndPoint);
                        var idx = Enumerable.Range(0, KnownPeers.Count).Cast<int?>().FirstOrDefault(x => KnownPeers[x ?? 0].GetHashCode() == peer.GetHashCode());
                        if (!idx.HasValue) return;

                        peer = KnownPeers[idx.Value];
                            peer.DisplayName = bin.ReadBoolean() ? bin.ReadString() : null;
                        if (bin.ReadBoolean())
                            bin.ReadBytes(bin.ReadInt32());

                        break;
                    }
                    case PeerCommand.BinaryBlob:
                    {
                        var pub = ReadAndVerifyPublicKey(stream);
                        if (pub == null)
                            return;
                        var peer = new PhonemePeer(this, pub, socket.RemoteEndPoint as IPEndPoint);
                        var idx = Enumerable.Range(0, KnownPeers.Count).Cast<int?>().FirstOrDefault(x => KnownPeers[x ?? 0].GetHashCode() == peer.GetHashCode());
                        if (!idx.HasValue) return;

                        peer = KnownPeers[idx.Value];
                        var blob = bin.ReadBytes(bin.ReadInt32());
                        using(var rsa = OpenSslKey.DecodeRsaPrivateKey(PrivateKey))
                            Console.WriteLine("{0}: {1}", peer.DisplayName ?? peer.EndPoint.ToString(), Encoding.ASCII.GetString(rsa.Decrypt(blob, false)));

                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private byte[] ReadAndVerifyPublicKey(Stream stream)
        {
            var bin = new BinaryReader(stream);

            var sig = bin.ReadBytes(bin.ReadInt32());
            var pubKey = bin.ReadBytes(bin.ReadInt32());

            using (var rsa = OpenSslKey.DecodeX509PublicKey(pubKey))
                return !rsa.VerifyData(pubKey, new SHA256CryptoServiceProvider(), sig) ? null : pubKey;
        }

        private void UdpRecieveLoop(IAsyncResult result)
        {
            _udpClient.BeginReceive(UdpRecieveLoop, null);

            IPEndPoint remote = null;
            var data = _udpClient.EndReceive(result, ref remote);

            if (remote == null || data == null || data.Length == 0)
                return;

            using (var mem = new MemoryStream(data))
            {
                var bin = new BinaryReader(mem);
                var sig = bin.ReadBytes(bin.ReadInt32());
                var pubKey = bin.ReadBytes(bin.ReadInt32());

                using (var rsa = OpenSslKey.DecodeX509PublicKey(pubKey))
                {
                    if (!rsa.VerifyData(pubKey, new SHA256CryptoServiceProvider(), sig)) return;

                    //if(pubKey.Compare(PublicKey))
                        //return;

                    var peer = new PhonemePeer(this, pubKey, remote);
                    if(KnownPeers.All(x => x.GetHashCode() != peer.GetHashCode()))
                        KnownPeers.Add(peer);
                    peer.PerformTcpHandshake();
                }
            }

        }
        
        public void Dispose()
        {
        }
    }
}
