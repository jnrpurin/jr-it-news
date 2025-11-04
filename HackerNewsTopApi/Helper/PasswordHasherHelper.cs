using System.Security.Cryptography;
using System.Text;

namespace HackerNewsTopApi.Helper
{
    public static class PasswordHasher
    {
        public static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        public static bool VerifyPassword(string password, string hash)
        {
            var hashed = HashPassword(password);
            return hashed == hash;
        }
    }
}
