using System.Security.Cryptography;
using AdvantageTool.Data;
using LtiAdvantage.Jwks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Services;

/// <summary>
/// Publishes the tool's public keys from the database: each platform registration has its
/// own key pair, plus the previous key while a rotation is in progress. Every key has its own
/// kid, so a platform picks the right one from the tool's single JWKS.
/// </summary>
public class PlatformJwksKeyStore(ApplicationDbContext context) : IJwksKeyStore
{
    public async Task<IReadOnlyList<JsonWebKey>> GetPublicKeysAsync()
    {
        var platforms = await context.Platforms.AsNoTracking().ToListAsync();

        var keys = new List<JsonWebKey>();
        foreach (var platform in platforms)
        {
            keys.Add(ToJwk(platform.PublicKey, platform.KeyId));
            if (!string.IsNullOrEmpty(platform.PreviousPublicKey) && !string.IsNullOrEmpty(platform.PreviousKeyId))
                keys.Add(ToJwk(platform.PreviousPublicKey, platform.PreviousKeyId));
        }
        return keys;
    }

    private static JsonWebKey ToJwk(string publicKeyPem, string keyId)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        // Sets kty, use=sig, alg=RS256, kid, n and e; only public parameters are read.
        return InMemoryJwksKeyStore.CreateRsaKey(rsa, keyId);
    }
}
