using System;
using System.Threading.Tasks;
using AdvantageTool.Data;
using IdentityModel;
using IdentityModel.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace AdvantageTool.Pages
{
    public class OidcLoginModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OidcLoginModel> _logger;

        public OidcLoginModel(ApplicationDbContext context, ILogger<OidcLoginModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        [BindProperty(Name = "iss", SupportsGet = true)]
        public string Issuer { get; set; }

        [BindProperty(Name = "login_hint", SupportsGet = true)]
        public string LoginHint { get; set; }

        [BindProperty(Name = "lti_message_hint", SupportsGet = true)]
        public string LtiMessageHint { get; set; }

        [BindProperty(Name = "target_link_uri", SupportsGet = true)]
        public string TargetLinkUri { get; set; }

        public async Task<IActionResult> OnGet()
        {
            if (string.IsNullOrWhiteSpace(Issuer))
            {
                _logger.LogError(new ArgumentNullException(nameof(Issuer)), $"{nameof(Issuer)} is missing.");
                return BadRequest();
            }

            if (string.IsNullOrWhiteSpace(LoginHint))
            {
                _logger.LogError(new ArgumentNullException(nameof(LoginHint)), $"{nameof(LoginHint)} is missing.");
                return BadRequest();
            }

            if (string.IsNullOrWhiteSpace(LtiMessageHint))
            {
                _logger.LogError(new ArgumentNullException(nameof(LoginHint)), $"{nameof(LtiMessageHint)} is missing.");
                return BadRequest();
            }

            if (string.IsNullOrWhiteSpace(TargetLinkUri))
            {
                _logger.LogError(new ArgumentNullException(nameof(LoginHint)), $"{nameof(TargetLinkUri)} is missing.");
                return BadRequest();
            }

            var platform = await _context.GetPlatformByIssuerAsync(Issuer);
            if (platform == null)
            {
                _logger.LogError($"Issuer not found [{Issuer}].");
                return BadRequest();
            }

            // 	RPs MUST verify the value of the target_link_uri to prevent being used as an open redirector to external sites.
            if (!Uri.TryCreate(TargetLinkUri, UriKind.Absolute, out var targetLinkUri))
            {
                _logger.LogError($"Invalid target_link_uri [{TargetLinkUri}].");
                return BadRequest();
            }

            if (targetLinkUri.Host != Request.Host.Host)
            {
                _logger.LogError($"Invalid target_link_uri [{TargetLinkUri}].");
                return BadRequest();
            }

            var ru = new RequestUrl(platform.AuthorizeUrl);
            var url = ru.CreateAuthorizeUrl
            (
                clientId: platform.ClientId,
                responseType: OidcConstants.ResponseTypes.IdToken,
                // Consider redirecting to a page (maybe this page?) that verifies state, then onto TargetLinkUri
                redirectUri: TargetLinkUri,
                responseMode: OidcConstants.ResponseModes.FormPost,
                scope: OidcConstants.StandardScopes.OpenId,
                // Consider checking state after redirect to make sure the state was not tampared with
                state: CryptoRandom.CreateUniqueId(),
                loginHint: LoginHint,
                // Consider checking nonce at launch to make sure the id_token came from this flow and not direct
                nonce: CryptoRandom.CreateUniqueId(),
                prompt: "none",
                extra: new { lti_message_hint = LtiMessageHint }
            );

            _logger.LogInformation("Requesting authorization.");

            return Redirect(url);
        }
    }
}