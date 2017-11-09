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

        //public BitmapSource ProfilePicture { get; }
        
        internal PhonemePeer(PhonemeClient client, byte[] publicKey, IPEndPoint endPoint)
        {
            Client = client;
            PublicKey = publicKey;
            EndPoint = endPoint;
        }

        internal void PerformTcpHandshake()
        {
            using (var client = new TcpClient())
            {
                client.Connect(EndPoint);
                using (var stream = client.GetStream())
                {
                    var rdr = new BinaryReader(stream);
                    var wrt = new BinaryWriter(stream);
                    wrt.Write((byte)PeerCommand.Identify);

                    WriteSignedPublicKey(stream);

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
                    var bin = new BinaryWriter(stream);
                    bin.Write((byte) PeerCommand.BinaryBlob);

                    WriteSignedPublicKey(stream);

                    using (var rsa = OpenSslKey.DecodeX509PublicKey(PublicKey))
                    {
                        var data = rsa.Encrypt(messageData, false);
                        bin.Write(data.Length);
                        bin.Write(data);
                    }
                }
            }
        }

        private void WriteSignedPublicKey(Stream output)
        {
            var bin = new BinaryWriter(output);

            using (var rsa = OpenSslKey.DecodeRsaPrivateKey(Client.PrivateKey))
            {
                var sig = rsa.SignData(Client.PublicKey, new SHA256CryptoServiceProvider());
                bin.Write(sig.Length);
                bin.Write(sig);
                bin.Write(Client.PublicKey.Length);
                bin.Write(Client.PublicKey);
            }
        }

        /// <inheritdoc />
        public override int GetHashCode() => Convert.ToBase64String(PublicKey).GetHashCode();

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is PhonemePeer && ((PhonemePeer)obj).GetHashCode() == GetHashCode();

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
