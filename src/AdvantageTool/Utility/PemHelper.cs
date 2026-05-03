using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Utility;

public static class PemHelper
{
    public sealed record RsaKeyPair(string PrivateKey, string PublicKey, string KeyId);

    public static RsaKeyPair GenerateRsaKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return new RsaKeyPair(
            rsa.ExportRSAPrivateKeyPem(),
            rsa.ExportSubjectPublicKeyInfoPem(),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant());
    }

    public static SigningCredentials SigningCredentialsFromPem(string privateKeyPem, string keyId)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var key = new RsaSecurityKey(rsa) { KeyId = keyId };
        return new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    }

    public static RSAParameters PublicParametersFromPem(string publicKeyPem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        return rsa.ExportParameters(false);
    }
}
