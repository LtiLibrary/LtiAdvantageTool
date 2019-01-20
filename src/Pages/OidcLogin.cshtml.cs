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
    [IgnoreAntiforgeryToken]
    public class OidcLoginModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly StateDbContext _stateContext;
        private readonly ILogger<OidcLoginModel> _logger;

        public OidcLoginModel(
            ApplicationDbContext context, 
            StateDbContext stateContext,
            ILogger<OidcLoginModel> logger)
        {
            _context = context;
            _stateContext = stateContext;
            _logger = logger;
        }

        /// <summary>
        /// Platform Issuer URL
        /// </summary>
        [BindProperty(Name = "iss", SupportsGet = true)]
        public string Issuer { get; set; }

        /// <summary>
        /// Opaque value that helps the platform identify the user
        /// </summary>
        [BindProperty(Name = "login_hint", SupportsGet = true)]
        public string LoginHint { get; set; }

        /// <summary>
        /// Opaque value that helps the platform identity the resource link
        /// </summary>
        [BindProperty(Name = "lti_message_hint", SupportsGet = true)]
        public string LtiMessageHint { get; set; }

        /// <summary>
        /// Tool's launch URL
        /// </summary>
        [BindProperty(Name = "target_link_uri", SupportsGet = true)]
        public string TargetLinkUri { get; set; }

        public async Task<IActionResult> OnGet()
        {
            return await OnGetOrPost();
        }

        public async Task<IActionResult> OnPost()
        {
            return await OnGetOrPost();
        }

        public async Task<IActionResult> OnGetOrPost()
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
                _logger.LogError(new ArgumentNullException(nameof(LtiMessageHint)), $"{nameof(LtiMessageHint)} is missing.");
                return BadRequest();
            }

            if (string.IsNullOrWhiteSpace(TargetLinkUri))
            {
                _logger.LogError(new ArgumentNullException(nameof(TargetLinkUri)), $"{nameof(TargetLinkUri)} is missing.");
                return BadRequest();
            }

            // Get the platform settings
            var platform = await _context.GetPlatformByIssuerAsync(Issuer);
            if (platform == null)
            {
                _logger.LogError($"Issuer not found [{Issuer}].");
                return BadRequest();
            }

            // RPs MUST verify the value of the target_link_uri to prevent being
            // used as an open redirector to external sites.
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

            // Create a unique nonce for this flow
            var nonce = CryptoRandom.CreateUniqueId();

            // Consider using a state JWT as described in
            // https://tools.ietf.org/html/draft-bradley-oauth-jwt-encoded-state-09
            var state = CryptoRandom.CreateUniqueId();

            // Store the nonce and flow so they can be validated when the id_token
            // is posted back to the tool by the Authorization Server.
            _stateContext.AddState(nonce, state);

            var ru = new RequestUrl(platform.AuthorizeUrl);
            var url = ru.CreateAuthorizeUrl
            (
                clientId: platform.ClientId,
                responseType: OidcConstants.ResponseTypes.IdToken,

                // POST the id_token directly to the tool's launch URL
                responseMode: OidcConstants.ResponseModes.FormPost,
                redirectUri: TargetLinkUri,

                // Per IMS guidance
                scope: OidcConstants.StandardScopes.OpenId,

                // Consider checking state after redirect to make sure the state was not tampared with
                state: state,

                // The userId
                loginHint: LoginHint,

                // Checking nonce at launch to make sure the id_token came from this flow and not direct
                nonce: nonce,

                // No user interaction
                prompt: "none",

                // The messagedId (i.e. resource link id or deep link id)
                extra: new { lti_message_hint = LtiMessageHint }
            );

            _logger.LogInformation("Requesting id_token.");

            return Redirect(url);
        }
    }
}