using System.Runtime.InteropServices;

namespace EnarcLabs.Phoneme.Binding
{
    internal static class GlobalExtensions
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int memcmp(byte[] b1, byte[] b2, long count);

        public static bool Compare(this byte[] a, byte[] b) => !(a == null || b == null || a.Length != b.Length) && memcmp(a, b, a.Length) == 0;
    }
}
