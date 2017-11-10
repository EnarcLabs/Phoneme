using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using EnarcLabs.Phoneme.Binding.Security;

namespace EnarcLabs.Phoneme.Binding
{
    public delegate void PeerMessageHandler(PhonemePeer source, byte[] data);

    public class PhonemePeer : INotifyPropertyChanged
    {
        public PhonemeClient Client { get; }

        /// <summary>
        /// The public key which identifies this peer.
        /// </summary>
        public byte[] PublicKey { get; }

        private string _displayName;
        public string DisplayName
        {
            get { return _displayName; }
            internal set
            {
                if(_displayName == value)
                    return;

                _displayName = value;
                OnPropertyChanged();
            }
        }

        public IPEndPoint EndPoint { get; }

        internal PhonemePeer(PhonemeClient client, byte[] publicKey, IPEndPoint endPoint)
        {
            Client = client;
            PublicKey = publicKey;
            EndPoint = endPoint;
        }

        internal void PerformTcpHandshake()
        {
            if (Client.SymetricKey == Guid.Empty)
                Client.SymetricKey = Guid.NewGuid();

            using (var client = new TcpClient())
            {
                client.Connect(EndPoint);
                using (var stream = client.GetStream())
                {
                    var rdr = new BinaryReader(stream);
                    var wrt = new BinaryWriter(stream);
                    wrt.Write((byte)PeerCommand.Identify);

                    wrt.Write(Client.PublicKey.Length);
                    wrt.Write(Client.PublicKey);

                    var sigGuid = rdr.ReadBytes(16);
                    using (var rsa = OpenSslKey.DecodeRsaPrivateKey(Client.PrivateKey))
                    {
                        var sig = rsa.SignData(sigGuid, new SHA256CryptoServiceProvider());
                        wrt.Write(sig.Length);
                        wrt.Write(sig);
                    }

                    using (var rsa = OpenSslKey.DecodeX509PublicKey(PublicKey))
                    {
                        var encSym = rsa.Encrypt(Client.SymetricKey.ToByteArray(), false);
                        wrt.Write(encSym.Length);
                        wrt.Write(encSym);
                    }

                    wrt.Write(Client.DisplayName != null);
                    if(Client.DisplayName != null)
                        wrt.Write(Client.DisplayName);
                    wrt.Write(Client.ProfilePicture != null);
                    if (Client.ProfilePicture != null)
                    {

                        wrt.Write(Client.ProfilePicture.Length);
                        wrt.Write(Client.ProfilePicture);
                    }

                    DisplayName = rdr.ReadBoolean() ? rdr.ReadString() : null;
                    if (rdr.ReadBoolean())
                        rdr.ReadBytes(rdr.ReadInt32());
                }
            }
        }

        public void SendMessage(byte[] messageData)
        {
            using (var client = new TcpClient())
            {
                client.Connect(EndPoint);
                using (var stream = client.GetStream())
                {
                    var wrt = new BinaryWriter(stream);
                    wrt.Write((byte) PeerCommand.BinaryBlob);

                    GlobalHelpers.ProveIdentity(stream, Client.PublicKey, Client.PrivateKey);
                    GlobalHelpers.OneTimePad(messageData, Client.SymetricKey);

                    wrt.Write(messageData.Length);
                    wrt.Write(messageData);
                }
            }
        }

        /// <inheritdoc />
        public override int GetHashCode() => Convert.ToBase64String(PublicKey).GetHashCode();

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is PhonemePeer && ((PhonemePeer) obj).PublicKey.Compare(PublicKey);

        internal void OnMessageRecieved(byte[] data) => MessageRecieved?.Invoke(this, data);

        public event PeerMessageHandler MessageRecieved;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    internal enum PeerCommand : byte
    {
        Identify = 0,
        BinaryBlob = 1
    }
}
