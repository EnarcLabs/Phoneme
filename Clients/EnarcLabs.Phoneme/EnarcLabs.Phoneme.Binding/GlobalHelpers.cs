using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using EnarcLabs.Phoneme.Binding.Security;

namespace EnarcLabs.Phoneme.Binding
{
    /// <summary>
    /// Contains useful utility functions.
    /// </summary>
    internal static class GlobalHelpers
    {
        /// <summary>
        /// Shakes hands with the client to prove that you own a private key for your claimed public key.
        /// </summary>
        /// <param name="stream">The stream to handshake on.</param>
        /// <param name="privateKey">The private key to use for signing.</param>
        public static void ProveIdentity(Stream stream, byte[] privateKey)
        {
            var rdr = new BinaryReader(stream);
            var wrt = new BinaryWriter(stream);

            var sigGuid = rdr.ReadBytes(16);
            using (var rsa = OpenSslKey.DecodeRsaPrivateKey(privateKey))
            {
                var sig = rsa.SignData(sigGuid, new SHA256CryptoServiceProvider());
                wrt.Write(sig.Length);
                wrt.Write(sig);
            }
        }

        /// <summary>
        /// Attempts to verify the public key on the other end of the stream.
        /// </summary>
        /// <param name="stream">The stream to handshake on.</param>
        /// <param name="publicKey">The public key claimed by the other side.</param>
        /// <returns>True if the identity was verified.</returns>
        public static bool VerifyIdentity(Stream stream, out byte[] publicKey)
        {
            var rdr = new BinaryReader(stream);

            var sigGuid = Guid.NewGuid();
            //Length is fixed - 16 bytes
            stream.Write(sigGuid.ToByteArray(), 0, 16);

            publicKey = rdr.ReadBytes(rdr.ReadInt32());
            var sig = rdr.ReadBytes(rdr.ReadInt32());
            using (var rsa = OpenSslKey.DecodeX509PublicKey(publicKey))
                return rsa.VerifyData(sigGuid.ToByteArray(), new SHA256CryptoServiceProvider(), sig);
        }

        public static void OneTimePad(byte[] data, Guid key) => OneTimePad(data, key.ToByteArray());

        /// <summary>
        /// Xors a lot of data with a key.
        /// </summary>
        /// <param name="data">The data to encrypt / decrypt.</param>
        /// <param name="key">The key to use for this process.</param>
        public static unsafe void OneTimePad(byte[] data, byte[] key)
        {
            fixed(byte* keyPtr = key)
            fixed (byte* ptr = data)
                for (var i = 0; i < data.Length; i++)
                    ptr[i] ^= keyPtr[i % key.Length];
        }

        /// <summary>
        /// Sends a packet to everyone on all interfaces.
        /// </summary>
        /// <param name="packet">The data to broadcast.</param>
        /// <param name="port">The port to broadcast on.</param>
        public static void BroadcastPacket(byte[] packet, int port)
        {
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

                    bClient.Send(packet, packet.Length,
                        new IPEndPoint(IPAddress.Broadcast, port));
                }
            }
        }
    }
}
