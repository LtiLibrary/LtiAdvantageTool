using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using AdvantageTool.Data;
using IdentityModel.Client;
using LtiAdvantage.IdentityModel.Client;
using LtiAdvantage.Lti;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Utility
{
    public class AccessTokenService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public AccessTokenService(ApplicationDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<TokenResponse> GetAccessTokenAsync(JwtSecurityToken securityToken, string scope)
        {
            var platform = await _context.GetPlatformByIssuerAsync(securityToken.Payload.Iss);
            if (platform == null)
            {
                return new TokenResponse(new Exception("Cannot find platform registration."));
            }

            var httpClient = _httpClientFactory.CreateClient();
            var tokenEndPoint = platform.AccessTokenUrl;

            if (tokenEndPoint.IsMissing())
            {
                var disco = await httpClient.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
                {
                    Address = platform.Issuer,
                    Policy =
                    {
                        Authority = platform.Issuer
                    }
                });
                if (disco.IsError)
                {
                    return new TokenResponse(new Exception(disco.Error));
                }

                tokenEndPoint = disco.TokenEndpoint;
            }

            // Use a signed JWT as client credentials.
            var payload = new JwtPayload();
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Iss, platform.ClientId));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, platform.ClientId));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Aud, tokenEndPoint));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(DateTime.UtcNow).ToString()));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Nbf, EpochTime.GetIntDate(DateTime.UtcNow.AddSeconds(-5)).ToString()));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Exp, EpochTime.GetIntDate(DateTime.UtcNow.AddMinutes(5)).ToString()));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Jti, LtiResourceLinkRequest.GenerateCryptographicNonce()));

            var credentials = PemHelper.SigningCredentialsFromPemString(platform.PrivateKey);
            var header = new JwtHeader(credentials);
            var token = new JwtSecurityToken(header, payload);
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.WriteToken(token);

            return await httpClient.RequestClientCredentialsTokenWithJwtAsync(
                    new JwtClientCredentialsTokenRequest
                    {
                        Address = tokenEndPoint,
                        Jwt = jwt,
                        Scope = scope
                    });
        }

    }
}
