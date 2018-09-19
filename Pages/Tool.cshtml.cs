using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using LtiAdvantageLibrary.NetCore.Lti;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Pages
{
    // Tool launches typically come from outsite this app. Order will not be required starting with AspNetCore 2.2.
    // See https://github.com/aspnet/Mvc/issues/7795#issuecomment-397071059
    [IgnoreAntiforgeryToken(Order = 1001)]
    public class ToolModel : PageModel
    {
        /// <summary>
        /// Get or set the error discovered while parsing the request.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Get or set the id_token (JWT) in the request. Platforms will always send the
        /// id_token of a launch request in the body of a form post.
        /// </summary>
        [BindProperty(Name = "id_token")]
        public string IdToken { get; set; }

        /// <summary>
        /// This is a wrapper around the JwtPayload that makes it easy to examine the
        /// claims. For example, LtiRequest.Roles gets the role claims as an Enum array
        /// so you don't have to match string values.
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
        [HttpPost]
        public IActionResult OnPost()
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

            // The Issuer Identifier for the Platform MUST exactly match the value of the iss (Issuer)
            // Claim (therefore the Tool MUST previously have been made aware of this identifier). The
            // Issuer Identifier was collected in an offline process.
            // See https://www.imsglobal.org/spec/security/v1p0/#dfn-issuer-identifier
            
            if (string.IsNullOrEmpty(Token.Issuer))
            {
                Error = "Issuer is missing from id_token";
                return Page();
            }

            // Look for the local record of this issuer
            
            var issuer = Token.Issuer; // Normally this would actually look up the issuer
            if (string.IsNullOrEmpty(issuer))
            {
                Error = $"Issuer '{issuer}' is not recognized.";
                return Page();
            }

            // The ID Token MUST contain a nonce Claim.

            var nonce = Token.Claims.SingleOrDefault(c => c.Type == "nonce")?.Value;
            if (string.IsNullOrEmpty(nonce))
            {
                Error = "Nonce is missing from id_token";
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

            // Prepare the TokenValidationParameters using information
            // gathered during previous registration process

            var publicKey =
@"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAxwNk5GjdXmb4iFWOe/Lf
kWYfuzUhU+rHef4FziWJq31RZUkdKjaul0MyUwPZ/u2Gpzpdr1hNSa3Kmtj4BQk8
IUgveVAyvNxTMinsEm6hSjihQHnM5LLWGM804uZ8ylS0Rt4ne31hIQSOnxBp6LXj
Uvxdavl5Zp+tt5aF+5zxE0Viu7s4oqwEdr25kCdo/H4zBadLGCmx1IFFYqd8voEM
AILwP02jbuOSeSxK86b2uxLl4BZb9qL1Itd2+Febtt8PW4vVkcl7jWXQUBhQRn1L
GNRmKF4nXZVVAYu1grC4jXqIYX0rY9BuQAgR3W1B+aBWfPCxkOFyCH5re6lNA+OH
oQIDAQAB
-----END PUBLIC KEY-----";
            var audiences = new [] {Request.GetDisplayUrl()};

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,

                ValidateAudience = true,
                ValidAudiences = audiences,
                
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new RsaSecurityKey(RsaHelper.PublicKeyFromPemString(publicKey)),

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

            LtiRequest = new LtiResourceLinkRequest(Token.Payload);

            return Page();
        }
    }
}
