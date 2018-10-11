using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using AdvantageTool.Data;
using LtiAdvantageLibrary.NetCore.Lti;
using LtiAdvantageLibrary.NetCore.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Pages
{
    // Tool launches typically come from outsite this app. Order will not be required starting with AspNetCore 2.2.
    // See https://github.com/aspnet/Mvc/issues/7795#issuecomment-397071059
    [IgnoreAntiforgeryToken(Order = 1001)]
    public class ToolModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ToolModel(ApplicationDbContext context)
        {
            _context = context;
        }

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
        public async Task<IActionResult> OnPost()
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

            // The Issuer Identifier for the Platform MUST exactly match the value of the Issuer (iss)
            // Claim (therefore the Tool MUST previously have been made aware of this identifier). The
            // Issuer Identifier is collected in an offline process.
            // See https://www.imsglobal.org/spec/security/v1p0/#dfn-issuer-identifier
            if (string.IsNullOrEmpty(Token.Issuer))
            {
                Error = "Issuer is missing from id_token";
                return Page();
            }
            var platform = await _context.Platforms.FindAsync(Token.Issuer);
            if (platform == null)
            {
                Error = $"Issuer '{Token.Issuer}' is not recognized.";
                return Page();
            }

            // The Audience Claim must match a Client ID exactly.
            if (!Token.Audiences.Any())
            {
                Error = "Audiences are missing from id_token";
            }

            Client client = null;
            foreach (var audience in Token.Audiences)
            {
                client = await _context.Clients.FindAsync(audience);
                if (client != null) break;
            }
            if (client == null)
            {
                Error = $"Audiences '{string.Join(", ", Token.Audiences)}' are not recogized.";
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

            var validationParameters = new TokenValidationParameters
            {
                ValidateAudience = false, // Done manually above
                ValidateIssuer = true,
                ValidIssuer = platform.Id,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new RsaSecurityKey(RsaHelper.PublicKeyFromPemString(platform.PublicKey)),

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

            // Save the updated current platform information
            platform.ContactEmail = LtiRequest.Platform.ContactEmail;
            platform.Description = LtiRequest.Platform.Description;
            platform.Guid = LtiRequest.Platform.Guid;
            platform.Name = LtiRequest.Platform.Name;
            platform.ProductFamilyCode = LtiRequest.Platform.ProductFamilyCode;
            platform.Url = LtiRequest.Platform.Url;
            platform.Version = LtiRequest.Platform.Version;
            _context.Attach(platform).State = EntityState.Modified;
            await _context.SaveChangesAsync();


            // Show something interesting to the platform user
            return Page();
        }
    }
}
