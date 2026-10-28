using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AdvantageTool.Data;
using IdentityModel;
using IdentityModel.Client;
using LtiAdvantage.IdentityModel.Client;
using Microsoft.IdentityModel.Tokens;

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

        var now = DateTime.UtcNow;
        var payload = new JwtPayload();
        payload.AddClaim(new Claim(JwtRegisteredClaimNames.Iss, platform.ClientId));
        payload.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, platform.ClientId));
        payload.AddClaim(new Claim(JwtRegisteredClaimNames.Aud, platform.AccessTokenUrl));
        payload.AddClaim(new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now).ToString()));
        payload.AddClaim(new Claim(JwtRegisteredClaimNames.Nbf, EpochTime.GetIntDate(now.AddSeconds(-5)).ToString()));
        payload.AddClaim(new Claim(JwtRegisteredClaimNames.Exp, EpochTime.GetIntDate(now.AddMinutes(5)).ToString()));
        payload.AddClaim(new Claim(JwtRegisteredClaimNames.Jti, CryptoRandom.CreateUniqueId()));

        var credentials = AdvantageTool.Utility.PemHelper.SigningCredentialsFromPem(platform.PrivateKey, platform.KeyId);
        var jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));

        var http = httpClientFactory.CreateClient();
        return await http.RequestClientCredentialsTokenWithJwtAsync(new JwtClientCredentialsTokenRequest
        {
            Address = platform.AccessTokenUrl,
            ClientId = platform.ClientId,
            // IdentityModel defaults to an Authorization header, which it refuses to
            // combine with a client assertion. LTI expects the assertion in the body.
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
            Jwt = jwt,
            Scope = scope,
        });
    }
}
