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
                _logger.LogError("Platform not found.");
                return NotFound();
            }

            var ru = new RequestUrl(platform.AuthorizeUrl);
            var url = ru.CreateAuthorizeUrl
            (
                clientId: platform.ClientId,
                responseType: OidcConstants.ResponseTypes.IdToken,
                redirectUri: TargetLinkUri,
                responseMode: OidcConstants.ResponseModes.FormPost,
                scope: OidcConstants.StandardScopes.OpenId,
                state: CryptoRandom.CreateUniqueId(),
                loginHint: LoginHint,
                nonce: CryptoRandom.CreateUniqueId(),
                extra: new { lti_message_hint = LtiMessageHint }
            );

            _logger.LogInformation("Requesting authorization.");

            return Redirect(url);
        }
    }
}