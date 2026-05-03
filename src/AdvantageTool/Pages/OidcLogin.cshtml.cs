using AdvantageTool.Data;
using IdentityModel;
using IdentityModel.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages;

[IgnoreAntiforgeryToken]
public class OidcLoginModel(
    ApplicationDbContext context,
    StateDbContext state,
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

        var platform = await context.GetPlatformByIssuerAsync(Issuer);
        if (platform is null)
        {
            logger.LogError("Unknown issuer {Issuer}.", Issuer);
            return BadRequest();
        }

        if (!Uri.TryCreate(TargetLinkUri, UriKind.Absolute, out var target) || target.Host != Request.Host.Host)
        {
            logger.LogError("Invalid target_link_uri {Target}.", TargetLinkUri);
            return BadRequest();
        }

        var nonce = CryptoRandom.CreateUniqueId();
        var stateValue = CryptoRandom.CreateUniqueId();
        state.AddState(nonce, stateValue);

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
