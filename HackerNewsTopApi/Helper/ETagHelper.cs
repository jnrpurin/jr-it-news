using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HackerNewsTopApi.Helper
{
    public static class ETagHelper
    {
        public static string ComputeETag<T>(T obj)
        {
            var json = JsonSerializer.Serialize(obj);
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(json);
            var hash = sha.ComputeHash(bytes);
            // base64 url-safe or hex
            return $"\"{Convert.ToBase64String(hash)}\"";
        }
    }
}
