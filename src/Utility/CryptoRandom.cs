using System;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Utility
{
    public static class CryptoRandom
    {
        private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

        public static string GenerateRandomString(int length = 8)
        {
            var buffer = new byte[length];
            Rng.GetBytes(buffer);
            return Base64UrlEncoder.Encode(buffer);
        }

        public static string GenerateRandomNumber()
        {
            var buffer = new byte[8];
            Rng.GetBytes(buffer);
            return BitConverter.ToUInt64(buffer).ToString();
        }
    }
}
