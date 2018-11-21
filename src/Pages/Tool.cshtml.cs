using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using IdentityModel.Client;
using LtiAdvantageLibrary;
using LtiAdvantageLibrary.Lti;
using LtiAdvantageLibrary.NamesRoleService;
using LtiAdvantageLibrary.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using JwtRegisteredClaimNames = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames;

namespace AdvantageTool.Pages
{
    // Tool launches typically come from outside this app. Order will not be required starting with AspNetCore 2.2.
    // See https://github.com/aspnet/Mvc/issues/7795#issuecomment-397071059
    [IgnoreAntiforgeryToken(Order = 1001)]
    public class ToolModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public ToolModel(ApplicationDbContext context, 
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// The client's id.
        /// </summary>
        [BindProperty]
        public int ClientId { get; set; }

        /// <summary>
        /// The error discovered while parsing the request.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// The id_token posted in the launch request.
        /// </summary>
        [BindProperty(Name = "id_token")]
        public string IdToken { get; set; }

        /// <summary>
        /// The platform's id.
        /// </summary>
        [BindProperty]
        public int PlatformId { get; set; }

        /// <summary>
        /// Wrapper around the request payload.
        /// </summary>
        public LtiResourceLinkRequest LtiRequest { get; set; }

        /// <summary>
        /// Results from NameRoleService.
        /// </summary>
        public MembershipContainer Membership { get; set; }
        public string MembershipStatus { get; set; }

        /// <summary>
        /// Get or set the JwtSecurityToken (id_token) in the request.
        /// </summary>
        public JwtSecurityToken Token { get; set; }

        /// <summary>
        /// Handle the LTI POST request to launch the tool. 
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> OnPostAsync()
        {
            // Authenticate the request starting at step 5 in the OpenId Implicit Flow
            // See https://www.imsglobal.org/spec/security/v1p0/#platform-originating-messages
            // See https://openid.net/specs/openid-connect-core-1_0.html#ImplicitFlowSteps

            // The Platform MUST send the id_token via the OAuth 2 Form Post
            // See https://www.imsglobal.org/spec/security/v1p0/#successful-authentication
            // See http://openid.net/specs/oauth-v2-form-post-response-mode-1_0.html

            if (string.IsNullOrEmpty(IdToken))
            {
                Error = "id_token is missing or empty";
                return Page();
            }

            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(IdToken))
            {
                Error = "Cannot read id_token";
                return Page();
            }

            Token = handler.ReadJwtToken(IdToken);

            // Authentication Response Validation
            // See https://www.imsglobal.org/spec/security/v1p0/#authentication-response-validation

            // The ID Token MUST contain a nonce Claim.
            var nonce = Token.Claims.SingleOrDefault(c => c.Type == "nonce")?.Value;
            if (string.IsNullOrEmpty(nonce))
            {
                Error = "Nonce is missing.";
                return Page();
            }

            // The Token must include a Payload
            if (Token.Payload == null)
            {
                Error = "Payload is missing.";
                return Page();
            }

            // The Audience must match a Client ID exactly.
            var client = await _context.Clients
                .Where(c => Token.Payload.Aud.Contains(c.ClientId))
                .SingleOrDefaultAsync();
            if (client == null)
            {
                Error = "Unknown audience.";
                return Page();
            }

            ClientId = client.Id;

            var platform = await _context.Platforms
                .Where(p => p.Issuer == Token.Payload.Iss)
                .SingleOrDefaultAsync();
            if (platform == null)
            {
                Error = "Unknown issuer.";
                return Page();
            }

            PlatformId = platform.Id;

            // Using the JwtSecurityTokenHandler.ValidateToken method, validate four things:
            //
            // 1. The Issuer Identifier for the Platform MUST exactly match the value of the iss
            //    (Issuer) Claim (therefore the Tool MUST previously have been made aware of this
            //    identifier.
            // 2. The Tool MUST Validate the signature of the ID Token according to JSON Web Signature
            //    RFC 7515, Section 5; using the Public Key for the Platform which collected offline.
            // 3. The Tool MUST validate that the aud (audience) Claim contains its client_id value
            //    registered as an audience with the Issuer identified by the iss (Issuer) Claim. The
            //    aud (audience) Claim MAY contain an array with more than one element. The Tool MUST
            //    reject the ID Token if it does not list the client_id as a valid audience, or if it
            //    contains additional audiences not trusted by the Tool.
            // 4. The current time MUST be before the time represented by the exp Claim;

            RSAParameters rsaParameters;

            try
            {
                if (!string.IsNullOrEmpty(platform.JsonWebKeySetUrl))
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var keySetJson = await httpClient.GetStringAsync(platform.JsonWebKeySetUrl);
                    var keySet = JsonConvert.DeserializeObject<JsonWebKeySet>(keySetJson);
                    var key = keySet.Keys.SingleOrDefault(k => k.Kid == Token.Header.Kid);
                    if (key == null)
                    {
                        Error = "No matching key found.";
                        return Page();
                    }

                    rsaParameters = new RSAParameters
                    {
                        Modulus = Base64UrlEncoder.DecodeBytes(key.N),
                        Exponent = Base64UrlEncoder.DecodeBytes(key.E)
                    };
                }
                else
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var disco = await httpClient.GetDiscoveryDocumentAsync(Token.Issuer);
                    if (disco.IsError)
                    {
                        Error = disco.Error;
                        return Page();
                    }

                    var key = disco.KeySet.Keys.SingleOrDefault(k => k.Kid == Token.Header.Kid);
                    if (key == null)
                    {
                        Error = "No matching key found.";
                        return Page();
                    }

                    rsaParameters = new RSAParameters
                    {
                        Modulus = Base64UrlEncoder.DecodeBytes(key.N),
                        Exponent = Base64UrlEncoder.DecodeBytes(key.E)
                    };
                }
            }
            catch (Exception e)
            {
                Error = e.Message;
                return Page();
            }

            var validationParameters = new TokenValidationParameters
            {
                ValidateTokenReplay = true,
                ValidateAudience = false, // Validated above
                ValidateIssuer = false, // Validated above
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new RsaSecurityKey(rsaParameters),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5.0)
            };

            try
            {
                handler.ValidateToken(IdToken, validationParameters, out _);
            }
            catch (Exception e)
            {
                Error = e.Message;
                return Page();
            }

            // Wrap the JwtPayload in an LtiResourceLinkRequest.
            LtiRequest = new LtiResourceLinkRequest(Token.Payload);

            // Show something interesting
            return Page();
        }

        /// <summary>
        /// Handler for requesting course members.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> OnPostNamesRoleServiceAsync()
        {
            var client = await _context.Clients.FindAsync(ClientId);
            if (client == null)
            {
                MembershipStatus = "Cannot find client registration.";
                return await OnPostAsync();
            }

            var platform = await _context.Platforms.FindAsync(PlatformId);
            if (platform == null)
            {
                MembershipStatus = "Cannot find platform registration.";
                return await OnPostAsync();
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
                    MembershipStatus = disco.Error;
                    return await OnPostAsync();
                }

                tokenEndPoint = disco.TokenEndpoint;
            }

            // Use a signed JWT as client credentials.
            var payload = new JwtPayload();
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Iss, client.ClientId));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, client.ClientId));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Aud, platform.AccessTokenUrl));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(DateTime.UtcNow).ToString()));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Nbf, EpochTime.GetIntDate(DateTime.UtcNow.AddSeconds(-5)).ToString()));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Exp, EpochTime.GetIntDate(DateTime.UtcNow.AddMinutes(5)).ToString()));
            payload.AddClaim(new Claim(JwtRegisteredClaimNames.Jti, LtiResourceLinkRequest.GenerateCryptographicNonce()));

            var credentials = PemHelper.SigningCredentialsFromPemString(client.PrivateKey, client.KeyId);
            var header = new JwtHeader(credentials);
            var token = new JwtSecurityToken(header, payload);
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.WriteToken(token);

            var tokenClient = new TokenClient(tokenEndPoint, client.ClientId);
            var tokenResponse = await tokenClient.RequestClientCredentialsWithSignedJwtAsync(jwt, Constants.LtiScopes.MembershipReadonly);

            // The IMS reference implementation returns "Created" with success. 
            if (tokenResponse.IsError && tokenResponse.Error != "Created")
            {
                MembershipStatus = tokenResponse.Error;
                return await OnPostAsync();
            }

            httpClient.SetBearerToken(tokenResponse.AccessToken);

            try
            {
                Token = handler.ReadJwtToken(IdToken);
                LtiRequest = new LtiResourceLinkRequest(Token.Payload);

                using (var response = await httpClient.GetAsync(LtiRequest.NamesRoleService.ContextMembershipUrl)
                    .ConfigureAwait(false))
                {
                    var content = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        Membership = JsonConvert.DeserializeObject<MembershipContainer>(content);
                    }
                    else
                    {
                        MembershipStatus = !string.IsNullOrEmpty(content) 
                            ? content
                            : response.ReasonPhrase;
                    }
                }
            }
            catch (Exception e)
            {
                MembershipStatus = e.Message;
            }

            return await OnPostAsync();
        }
    }
}
