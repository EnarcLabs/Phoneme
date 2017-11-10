using System.IO;
using System.Linq;

namespace EnarcLabs.Phoneme.Binding
{
    /// <summary>
    /// Verifies that any user wishing to join the mesh is trusted by this client.
    /// </summary>
    public interface ITrustVerifier
    {
        /// <summary>
        /// Verifies the public key to ensure the specified user is trusted.
        /// </summary>
        /// <param name="publicKey">The public key to verify.</param>
        /// <returns>True if the user should be trusted, false otherwise.</returns>
        bool VerifyTrust(byte[] publicKey);
    }

    /// <inheritdoc />
    /// <summary>
    /// Reads PEM-formatted files from a directory to verify user trust.
    /// </summary>
    public class DirectoryTrustVerifier : ITrustVerifier
    {
        /// <summary>
        /// The path to search for public key files.
        /// </summary>
        public string FilePath { get; }
        
        public DirectoryTrustVerifier(string filePath)
        {
            FilePath = filePath;
        }

        public bool VerifyTrust(byte[] publicKey)
        {
            var pem = new PemFile();
            return Directory.EnumerateFiles(FilePath).Any(file => pem.ReadFile(file) && pem.Key.Compare(publicKey));
        }
    }
}
