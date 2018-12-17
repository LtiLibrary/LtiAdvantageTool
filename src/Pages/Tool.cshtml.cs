using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using IdentityModel.Client;
using LtiAdvantage;
using LtiAdvantage.AssignmentGradeServices;
using LtiAdvantage.Lti;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace AdvantageTool.Pages
{
    // Tool launches typically come from outside this app and from unknown places, so
    // I disable the anti-forgery token. Order will not be required starting with AspNetCore 2.2.
    // See https://github.com/aspnet/Mvc/issues/7795#issuecomment-397071059
    [IgnoreAntiforgeryToken(Order = 1001)]
    public class ToolModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly AccessTokenService _accessTokenService;
        private readonly IHttpClientFactory _httpClientFactory;

        public ToolModel(
            ApplicationDbContext context,
            AccessTokenService accessTokenService,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _accessTokenService = accessTokenService;
            _httpClientFactory = httpClientFactory;
        }

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
        /// Wrapper around the request payload.
        /// </summary>
        public LtiResourceLinkRequest LtiRequest { get; set; }

        /// <summary>
        /// Get or set the JwtSecurityToken (id_token) in the request.
        /// </summary>
        public JwtSecurityToken Token { get; set; }

        /// <summary>
        /// Handle the LTI POST request to launch the tool. 
        /// </summary>
        /// <returns></returns>
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
            LtiRequest = new LtiResourceLinkRequest(Token.Payload);

            // Authentication Response Validation
            // See https://www.imsglobal.org/spec/security/v1p0/#authentication-response-validation

            // The ID Token MUST contain a nonce Claim.
            var nonce = Token.Claims.SingleOrDefault(c => c.Type == "nonce")?.Value;
            if (string.IsNullOrEmpty(nonce))
            {
                Error = "Nonce is missing.";
                return Page();
            }

            // The Audience must match a Client ID exactly.
            var platform = await _context.GetPlatformByIssuerAndAudienceAsync(Token.Payload.Iss, Token.Payload.Aud);
            if (platform == null)
            {
                Error = "Unknown issuer/audience.";
                return Page();
            }

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
                if (!string.IsNullOrEmpty(platform.JwkSetUrl))
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var keySetJson = await httpClient.GetStringAsync(platform.JwkSetUrl);
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

            // Show something interesting
            return Page();
        }

        /// <summary>
        /// Handler for creating a line item.
        /// </summary>
        /// <returns>The result.</returns>
        public async Task<IActionResult> OnPostCreateLineItemAsync(string idToken)
        {
            if (idToken.IsMissing())
            {
                Error = $"{nameof(idToken)} is missing.";
                return await OnPostAsync();
            }
            IdToken = idToken;

            var handler = new JwtSecurityTokenHandler();
            Token = handler.ReadJwtToken(IdToken);
            LtiRequest = new LtiResourceLinkRequest(Token.Payload);

            var tokenResponse = await _accessTokenService.GetAccessTokenAsync(
                Token.Payload.Iss, 
                Constants.LtiScopes.AgsLineItem);

            // The IMS reference implementation returns "Created" with success. 
            if (tokenResponse.IsError && tokenResponse.Error != "Created")
            {
                Error = tokenResponse.Error;
                return await OnPostAsync();
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.SetBearerToken(tokenResponse.AccessToken);
            httpClient.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue(Constants.MediaTypes.LineItem));

            try
            {
                var lineItem = new LineItem
                {
                    EndDateTime = DateTime.UtcNow.AddMonths(3),
                    Label = LtiRequest.ResourceLink.Title,
                    ResourceLinkId = LtiRequest.ResourceLink.Id,
                    ScoreMaximum = 100,
                    StartDateTime = DateTime.UtcNow
                };

                using (var response = await httpClient.PostAsync(
                        LtiRequest.AssignmentGradeServices.LineItemsUrl,
                        new StringContent(JsonConvert.SerializeObject(lineItem), Encoding.UTF8, Constants.MediaTypes.LineItem))
                    .ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Error = response.ReasonPhrase;
                    }
                }
            }
            catch (Exception e)
            {
                Error = e.Message;
            }

            return await OnPostAsync();
        }

        /// <summary>
        /// Handler for posting a score.
        /// </summary>
        /// <returns>The posted score.</returns>
        public async Task<IActionResult> OnPostPostScoreAsync(string idToken, string lineItemUrl)
        {
            if (idToken.IsMissing())
            {
                Error = $"{nameof(idToken)} is missing.";
                return await OnPostAsync();
            }
            IdToken = idToken;

            if (lineItemUrl.IsMissing())
            {
                Error = $"{nameof(lineItemUrl)} is missing.";
                return await OnPostAsync();
            }

            var handler = new JwtSecurityTokenHandler();
            Token = handler.ReadJwtToken(IdToken);
            LtiRequest = new LtiResourceLinkRequest(Token.Payload);

            var tokenResponse = await _accessTokenService.GetAccessTokenAsync(
                Token.Payload.Iss, 
                Constants.LtiScopes.AgsScore);

            // The IMS reference implementation returns "Created" with success. 
            if (tokenResponse.IsError && tokenResponse.Error != "Created")
            {
                Error = tokenResponse.Error;
                return await OnPostAsync();
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.SetBearerToken(tokenResponse.AccessToken);
            httpClient.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue(Constants.MediaTypes.Score));

            try
            {
                var score = new Score
                {
                    ActivityProgress = ActivityProgress.Completed,
                    GradingProgress = GradingProgess.FullyGraded,
                    ScoreGiven = new Random().NextDouble() * 100,
                    ScoreMaximum = 100,
                    TimeStamp = DateTime.UtcNow,
                    UserId = LtiRequest.UserId
                };
                if (score.ScoreGiven > 75)
                {
                    score.Comment = "Good job!";
                }

                using (var response = await httpClient.PostAsync(
                        lineItemUrl.EnsureTrailingSlash() + "scores",
                        new StringContent(JsonConvert.SerializeObject(score), Encoding.UTF8, Constants.MediaTypes.Score))
                    .ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Error = response.ReasonPhrase;
                    }
                }
            }
            catch (Exception e)
            {
                Error = e.Message;
            }

            return await OnPostAsync();
        }
    }
}
