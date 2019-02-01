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
using IdentityModel.Internal;
using LtiAdvantage;
using LtiAdvantage.AssignmentGradeServices;
using LtiAdvantage.Lti;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace AdvantageTool.Pages
{
    // Tool launches typically come from outside this app and from unknown places, so
    // I disable the anti-forgery token.
    [IgnoreAntiforgeryToken]
    public class ToolModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly StateDbContext _stateContext;
        private readonly AccessTokenService _accessTokenService;
        private readonly IHttpClientFactory _httpClientFactory;

        public ToolModel(
            ApplicationDbContext context,
            StateDbContext stateContext,
            AccessTokenService accessTokenService,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _stateContext = stateContext;
            _accessTokenService = accessTokenService;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// The error discovered while parsing the request.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// A copy of the id_token for diagnostic purposes.
        /// </summary>
        public string IdToken { get; set; }

        /// <summary>
        /// The parsed JWT header from id_token. Null if invalid token.
        /// </summary>
        public JwtHeader JwtHeader { get; set; }

        /// <summary>
        /// Wrapper around the request payload.
        /// </summary>
        public LtiResourceLinkRequest LtiRequest { get; set; }

        /// <summary>
        /// Handle the LTI POST request from the Authorization Server. 
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> OnPostAsync(
            string platformId,
            [FromForm(Name = "id_token")] string idToken, 
            [FromForm(Name = "scope")] string scope = null, 
            [FromForm(Name = "state")] string state = null, 
            [FromForm(Name = "session_state")] string sessionState = null)
        {
            // Authenticate the request starting at step 5 in the OpenId Implicit Flow
            // See https://www.imsglobal.org/spec/security/v1p0/#platform-originating-messages
            // See https://openid.net/specs/openid-connect-core-1_0.html#ImplicitFlowSteps

            // The Platform MUST send the id_token via the OAuth 2 Form Post
            // See https://www.imsglobal.org/spec/security/v1p0/#successful-authentication
            // See http://openid.net/specs/oauth-v2-form-post-response-mode-1_0.html

            if (string.IsNullOrEmpty(idToken))
            {
                Error = "id_token is missing or empty";
                return Page();
            }

            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(idToken))
            {
                Error = "Cannot read id_token";
                return Page();
            }

            var jwt = handler.ReadJwtToken(idToken);
            JwtHeader = jwt.Header;

            var messageType = jwt.Claims.SingleOrDefault(c => c.Type == Constants.LtiClaims.MessageType)?.Value;
            if (messageType.IsMissing())
            {
                Error = $"{Constants.LtiClaims.MessageType} claim is missing.";
                return Page();
            }

            // Authentication Response Validation
            // See https://www.imsglobal.org/spec/security/v1p0/#authentication-response-validation

            // The ID Token MUST contain a nonce Claim.
            var nonce = jwt.Claims.SingleOrDefault(c => c.Type == "nonce")?.Value;
            if (string.IsNullOrEmpty(nonce))
            {
                Error = "Nonce is missing from request.";
                return Page();
            }

            // If the launch was initiated with a 3rd party login, then there will be a state
            // entry for the nonce.
            var memorizedState = _stateContext.GetState(nonce);
            if (memorizedState == null)
            {
                Error = "Invalid nonce. Possible request replay.";
                return Page();
            }

            // The state should be echoed back by the AS without modification
            if (memorizedState.Value != state)
            {
                Error = "Invalid state.";
                return Page();
            }

            // Look for the platform with platformId in the redirect URI
            var platform = await _context.GetPlatformByPlatformId(platformId);
            if (platform == null)
            {
                Error = "Unknown platform.";
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
                var httpClient = _httpClientFactory.CreateClient();
                var keySetJson = await httpClient.GetStringAsync(platform.JwkSetUrl);
                var keySet = JsonConvert.DeserializeObject<JsonWebKeySet>(keySetJson);
                var key = keySet.Keys.SingleOrDefault(k => k.Kid == jwt.Header.Kid);
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
            catch (Exception e)
            {
                Error = e.Message;
                return Page();
            }

            var validationParameters = new TokenValidationParameters
            {
                ValidateTokenReplay = true,
                ValidateAudience = true,
                ValidateIssuer = true,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,

                ValidAudience = platform.ClientId,
                ValidIssuer = platform.Issuer,
                IssuerSigningKey = new RsaSecurityKey(rsaParameters),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5.0)
            };

            try
            {
                handler.ValidateToken(idToken, validationParameters, out _);
            }
            catch (Exception e)
            {
                Error = e.Message;
                return Page();
            }

            if (messageType == Constants.Lti.LtiDeepLinkingRequestMessageType)
            {
                return Post("./Catalog", new { idToken });
            }

            IdToken = idToken;
            LtiRequest = new LtiResourceLinkRequest(jwt.Payload);

            return Page();
        }

        /// <summary>
        /// Handler for creating a line item.
        /// </summary>
        /// <returns>The result.</returns>
        public async Task<IActionResult> OnPostCreateLineItemAsync([FromForm(Name = "id_token")] string idToken)
        {
            if (idToken.IsMissing())
            {
                Error = $"{nameof(idToken)} is missing.";
                return Page();
            }

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(idToken);
            LtiRequest = new LtiResourceLinkRequest(jwt.Payload);

            var tokenResponse = await _accessTokenService.GetAccessTokenAsync(
                LtiRequest.Iss, 
                Constants.LtiScopes.Ags.LineItem);

            // The IMS reference implementation returns "Created" with success. 
            if (tokenResponse.IsError && tokenResponse.Error != "Created")
            {
                Error = tokenResponse.Error;
                return Page();
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
                        new StringContent(JsonConvert.SerializeObject(lineItem), Encoding.UTF8, Constants.MediaTypes.LineItem)))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Error = response.ReasonPhrase;
                        return Page();
                    }
                }
            }
            catch (Exception e)
            {
                Error = e.Message;
                return Page();
            }

            return Relaunch(
                LtiRequest.Iss,
                LtiRequest.UserId,
                LtiRequest.ResourceLink.Id,
                LtiRequest.Context.Id);
        }

        /// <summary>
        /// Handler for posting a score.
        /// </summary>
        /// <returns>The posted score.</returns>
        public async Task<IActionResult> OnPostPostScoreAsync([FromForm(Name = "id_token")] string idToken, string lineItemUrl)
        {
            if (idToken.IsMissing())
            {
                Error = $"{nameof(idToken)} is missing.";
                return Page();
            }

            if (lineItemUrl.IsMissing())
            {
                Error = $"{nameof(lineItemUrl)} is missing.";
                return Page();
            }

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(idToken);
            LtiRequest = new LtiResourceLinkRequest(jwt.Payload);

            var tokenResponse = await _accessTokenService.GetAccessTokenAsync(
                LtiRequest.Iss, 
                Constants.LtiScopes.Ags.Score);

            // The IMS reference implementation returns "Created" with success. 
            if (tokenResponse.IsError && tokenResponse.Error != "Created")
            {
                Error = tokenResponse.Error;
                return Page();
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
                        new StringContent(JsonConvert.SerializeObject(score), Encoding.UTF8, Constants.MediaTypes.Score)))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Error = response.ReasonPhrase;
                        return Page();
                    }
                }
            }
            catch (Exception e)
            {
                Error = e.Message;
                return Page();
            }

            return Relaunch(
                LtiRequest.Iss,
                LtiRequest.UserId,
                LtiRequest.ResourceLink.Id,
                LtiRequest.Context.Id);
        }

        private RedirectResult Relaunch(string iss, string userId, string resourceLinkId, string contextId)
        {
            // Send request to tool's endpoint to initiate login
            var values = new
            {
                // The issuer identifier for the platform
                iss,

                // The platform identifier for the user to login
                login_hint = userId,

                // The endpoint to be executed at the end of the OIDC authentication flow
                target_link_uri = Url.Page("./Tool", null, null, Request.Scheme),

                // The identifier of the LtiResourceLink message (or the deep link message, etc)
                lti_message_hint = JsonConvert.SerializeObject(new
                {
                    id = resourceLinkId, 
                    messageType = Constants.Lti.LtiResourceLinkRequestMessageType, 
                    courseId = contextId
                })
            };

            var url = new RequestUrl(Url.Page("./OidcLogin")).Create(values);
            return Redirect(url);
        }
        
        /// <summary>
        /// Return a <see cref="ContentResult"/> that automatically POSTs the values.
        /// </summary>
        /// <param name="url">Where to post the values.</param>
        /// <param name="values">The values to post.</param>
        /// <returns></returns>
        private ContentResult Post(string url, object values)
        {
            var response = HttpContext.Response;
            response.Clear();

            var dictionary = ValuesHelper.ObjectToDictionary(values);

            var s = new StringBuilder();
            s.Append("<html><head><title></title></head>");
            s.Append("<body onload='document.forms[\"form\"].submit()'>");
            s.Append($"<form name='form' action='{url}' method='post'>");
            foreach (var (key, value) in dictionary)
            {
                s.Append($"<input type='hidden' name='{key}' value='{value}' />");
            }
            s.Append("</form></body></html>");
            return new ContentResult
            {
                Content = s.ToString(), 
                ContentType = "text/html", 
                StatusCode = StatusCodes.Status200OK
            };
        }
    }
}
