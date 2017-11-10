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
}
