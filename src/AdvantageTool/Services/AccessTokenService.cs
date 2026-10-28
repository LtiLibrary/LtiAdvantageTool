using AdvantageTool.Data;
using IdentityModel.Client;
using LtiAdvantage.IdentityModel.Client;

namespace AdvantageTool.Services;

public class AccessTokenService(ApplicationDbContext context, IHttpClientFactory httpClientFactory)
{
    public async Task<TokenResponse> GetAccessTokenAsync(string platformId, string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return ProtocolResponse.FromException<TokenResponse>(new ArgumentNullException(nameof(scope)));

        // By registration, not by issuer: one issuer can have several client ids.
        var platform = await context.GetPlatformByPlatformIdAsync(platformId);
        if (platform is null)
            return ProtocolResponse.FromException<TokenResponse>(new InvalidOperationException("Platform not registered"));

        // The library signs the client assertion with this registration's key (its kid in the
        // header, so the platform picks the right key from the tool's JWKS) and sends it in the
        // request body, as LTI expects.
        return await AccessTokenUtil.GetAccessTokenAsync(
            httpClientFactory.CreateClient(),
            platform.Issuer,
            scope.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            platform.ClientId,
            platform.AccessTokenUrl,
            platform.PrivateKey,
            platform.KeyId);
    }
}
