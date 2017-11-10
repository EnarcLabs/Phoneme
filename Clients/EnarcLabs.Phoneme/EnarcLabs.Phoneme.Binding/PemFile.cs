using System;
using System.IO;
using System.Text.RegularExpressions;

namespace EnarcLabs.Phoneme.Binding
{
    /// <summary>
    /// Represents an OpenSSL public or private key stored in PEM format.
    /// </summary>
    public class PemFile
    {
        /// <summary>
        /// The type of key.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The binary contents of the key file.
        /// </summary>
        public byte[] Key { get; set; }

        public bool ReadFile(string path)
        {
            var match = Regex.Matches(File.ReadAllText(path), @"-----BEGIN (?<header>[\w\s]+) KEY-----\n(?<key>[\s\S]*)\n-----END \1 KEY-----",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (match.Count < 1)
                return false;
            Type = match[0].Groups["header"].Value;
            Key = Convert.FromBase64String(match[0].Groups["key"].Value.Replace("\n", "").Replace("\r", ""));
            return true;
        }

        public void WriteFile(string path)
        {
            File.WriteAllText(path, string.Format(@"-----BEGIN {0} KEY-----\n{1}\n-----END {0} KEY-----", Type.ToUpper(), Convert.ToBase64String(Key)));
        }
    }
}
