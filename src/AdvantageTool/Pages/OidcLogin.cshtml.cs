using AdvantageTool.Data;
using AdvantageTool.Services;
using IdentityModel;
using IdentityModel.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages;

[IgnoreAntiforgeryToken]
public class OidcLoginModel(
    ApplicationDbContext context,
    ILogger<OidcLoginModel> logger) : PageModel
{
    [BindProperty(Name = "iss", SupportsGet = true)] public string? Issuer { get; set; }
    [BindProperty(Name = "login_hint", SupportsGet = true)] public string? LoginHint { get; set; }
    [BindProperty(Name = "lti_message_hint", SupportsGet = true)] public string? LtiMessageHint { get; set; }
    [BindProperty(Name = "target_link_uri", SupportsGet = true)] public string? TargetLinkUri { get; set; }
    [BindProperty(Name = "client_id", SupportsGet = true)] public string? ClientId { get; set; }

    public Task<IActionResult> OnGetAsync() => HandleAsync();
    public Task<IActionResult> OnPostAsync() => HandleAsync();

    private async Task<IActionResult> HandleAsync()
    {
        if (string.IsNullOrWhiteSpace(Issuer)
            || string.IsNullOrWhiteSpace(LoginHint)
            || string.IsNullOrWhiteSpace(TargetLinkUri))
        {
            logger.LogError("OIDC login missing required parameters.");
            return BadRequest();
        }

        // One issuer can have several registrations, so look the registration up by issuer and
        // client_id. client_id is optional in the login request; without it the issuer must be
        // unambiguous.
        Platform? platform;
        if (!string.IsNullOrWhiteSpace(ClientId))
        {
            platform = await context.GetPlatformAsync(Issuer, ClientId);
        }
        else
        {
            var candidates = await context.GetPlatformsByIssuerAsync(Issuer);
            platform = candidates.Count == 1 ? candidates[0] : null;
        }
        if (platform is null)
        {
            logger.LogError("No single registration for issuer {Issuer} and client id {ClientId}.", Issuer, ClientId);
            return BadRequest();
        }

        if (!Uri.TryCreate(TargetLinkUri, UriKind.Absolute, out var target) || target.Host != Request.Host.Host)
        {
            logger.LogError("Invalid target_link_uri {Target}.", TargetLinkUri);
            return BadRequest();
        }

        var nonce = CryptoRandom.CreateUniqueId();
        var stateValue = CryptoRandom.CreateUniqueId();
        LtiStateCookie.Set(Response, stateValue, nonce);

        var url = new RequestUrl(platform.AuthorizeUrl).CreateAuthorizeUrl(
            clientId: platform.ClientId,
            responseType: OidcConstants.ResponseTypes.IdToken,
            responseMode: OidcConstants.ResponseModes.FormPost,
            redirectUri: TargetLinkUri,
            scope: OidcConstants.StandardScopes.OpenId,
            state: stateValue,
            loginHint: LoginHint,
            nonce: nonce,
            prompt: "none",
            extra: string.IsNullOrEmpty(LtiMessageHint)
                ? null
                : new Parameters(new[] { new KeyValuePair<string, string>("lti_message_hint", LtiMessageHint) }));

        return Redirect(url);
    }
}
