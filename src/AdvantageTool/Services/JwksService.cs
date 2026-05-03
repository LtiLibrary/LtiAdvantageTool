using AdvantageTool.Data;
using AdvantageTool.Utility;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Services;

public class JwksService(ApplicationDbContext context)
{
    public async Task<object?> GetJwksAsync(string platformId)
    {
        var platform = await context.GetPlatformByPlatformIdAsync(platformId);
        if (platform is null || string.IsNullOrEmpty(platform.PublicKey)) return null;

        var rsaParams = PemHelper.PublicParametersFromPem(platform.PublicKey);
        var key = new RsaSecurityKey(rsaParams) { KeyId = platform.KeyId };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Use = "sig";
        jwk.Alg = SecurityAlgorithms.RsaSha256;
        return new { keys = new[] { jwk } };
    }
}
