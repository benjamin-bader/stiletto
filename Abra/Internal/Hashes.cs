using System.Security.Cryptography;
using System.Text;

namespace Abra.Internal
{
    public static class Hashes
    {
        public static string HashIdentifier(string identifier)
        {
            using (var sha = SHA1.Create()) {
                var bytes = sha.ComputeHash(Encoding.Unicode.GetBytes(identifier));
                var sb = new StringBuilder();
                for (var i = 0; i < bytes.Length; ++i) {
                    sb.Append(bytes[i].ToString("X"));
                }
                var startIndex = 0;
                while (char.IsNumber(sb[startIndex])) {
                    ++startIndex;
                }

                return sb.ToString(startIndex, sb.Length);
            }
        }
    }
}
