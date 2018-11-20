using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Utility
{
    public static class StringGenerator
    {
        public static string GenerateRandomString(int length = 24)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var buffer = new byte[length];
                rng.GetBytes(buffer);
                return Base64UrlEncoder.Encode(buffer);
            }
        }
    }
}
