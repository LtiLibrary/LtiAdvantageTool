using AdvantageTool.Data;
using IdentityModel;
using LtiAdvantage;
using LtiAdvantage.DynamicRegistration;
using LtiAdvantage.IdentityModel.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages;

/// <summary>
/// LTI Dynamic Registration, tool side. The platform opens this page (in an iframe or a popup)
/// with openid_configuration and registration_token. The tool reads the platform's configuration,
/// registers itself, stores the result as a Platform, and tells the platform it is done with the
/// org.imsglobal.lti.close postMessage.
/// </summary>
/// <remarks>
/// Only a signed-in tool admin can register: the URL arrives from whoever sends the browser
/// here, and registering creates a trusted platform (Dynamic Registration §3.3).
/// </remarks>
public class DynamicRegistrationModel(
    ApplicationDbContext context,
    IHttpClientFactory httpClientFactory,
    ILogger<DynamicRegistrationModel> logger) : PageModel
{
    [BindProperty(Name = "openid_configuration", SupportsGet = true)] public string? OpenIdConfiguration { get; set; }
    [BindProperty(Name = "registration_token", SupportsGet = true)] public string? RegistrationToken { get; set; }

    public PlatformOpenIdConfiguration? PlatformConfiguration { get; private set; }
    public Platform? Registered { get; private set; }
    public string? Error { get; private set; }

    /// <summary>Shows which platform is asking, so the admin can confirm before registering.</summary>
    public async Task<IActionResult> OnGetAsync()
    {
        PlatformConfiguration = await FetchConfigurationAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        PlatformConfiguration = await FetchConfigurationAsync();
        if (PlatformConfiguration is null) return Page();
        if (string.IsNullOrEmpty(PlatformConfiguration.RegistrationEndpoint))
        {
            Error = "The platform configuration has no registration_endpoint.";
            return Page();
        }

        // The tool's identity for this platform: a new id (part of the launch URL) and key pair.
        var platformId = CryptoRandom.CreateUniqueId(8);
        var keys = Utility.PemHelper.GenerateRsaKeyPair();

        ToolConfiguration registered;
        try
        {
            var http = httpClientFactory.CreateClient();
            registered = await http.RegisterToolAsync(
                PlatformConfiguration.RegistrationEndpoint, RegistrationToken,
                BuildToolConfiguration(PlatformConfiguration, platformId));
        }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException or ArgumentException)
        {
            logger.LogWarning(e, "Dynamic registration failed.");
            Error = $"The platform rejected the registration: {e.Message}";
            return Page();
        }

        var user = await context.GetUserAsync(User);
        Registered = new Platform
        {
            Name = PlatformConfiguration.LtiPlatformConfiguration?.ProductFamilyCode ?? new Uri(PlatformConfiguration.Issuer).Host,
            Issuer = PlatformConfiguration.Issuer,
            AuthorizeUrl = PlatformConfiguration.AuthorizationEndpoint,
            AccessTokenUrl = PlatformConfiguration.TokenEndpoint,
            JwkSetUrl = PlatformConfiguration.JwksUri,
            PlatformId = platformId,
            // The platform assigns the client id, and usually a deployment id (§3.6).
            ClientId = registered.ClientId,
            DeploymentIds = registered.LtiToolConfiguration?.DeploymentId ?? string.Empty,
            KeyId = keys.KeyId,
            PrivateKey = keys.PrivateKey,
            PublicKey = keys.PublicKey,
            UserId = user!.Id,
        };
        context.Platforms.Add(Registered);
        await context.SaveChangesAsync();
        return Page();
    }

    private async Task<PlatformOpenIdConfiguration?> FetchConfigurationAsync()
    {
        if (string.IsNullOrWhiteSpace(OpenIdConfiguration))
        {
            Error = "openid_configuration is missing. Start the registration from the platform.";
            return null;
        }

        try
        {
            // Also checks the URL (https, or http on localhost) and that the issuer in the
            // document matches the URL it came from (§3.5.1); a mismatch must abort.
            return await httpClientFactory.CreateClient().GetPlatformOpenIdConfigurationAsync(OpenIdConfiguration);
        }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException)
        {
            logger.LogWarning(e, "Could not read the platform configuration.");
            Error = $"Could not read the platform configuration: {e.Message}";
            return null;
        }
    }

    /// <summary>The registration request (§3.5.2): OIDC client metadata plus the LTI tool configuration.</summary>
    private ToolConfiguration BuildToolConfiguration(PlatformOpenIdConfiguration platform, string platformId)
    {
        var launchUrl = Url.Page("/Tool", null, new { platformId }, Request.Scheme)!;

        // Only ask for scopes and message types the platform says it supports.
        var scopes = new[]
        {
            Constants.LtiScopes.Ags.LineItem,
            Constants.LtiScopes.Ags.ResultReadonly,
            Constants.LtiScopes.Ags.Score,
            Constants.LtiScopes.Nrps.MembershipReadonly,
        }.Where(s => platform.ScopesSupported is null || platform.ScopesSupported.Contains(s));

        var messages = new List<MessageDescriptor>
        {
            new() { Type = Constants.Lti.LtiResourceLinkRequestMessageType },
            new() { Type = Constants.Lti.LtiDeepLinkingRequestMessageType, TargetLinkUri = launchUrl, Label = "Pick activities" },
        };
        var supported = platform.LtiPlatformConfiguration?.MessagesSupported?.Select(m => m.Type).ToHashSet();
        if (supported is not null) messages.RemoveAll(m => !supported.Contains(m.Type));

        return new ToolConfiguration
        {
            ApplicationType = "web",
            ResponseTypes = ["id_token"],
            GrantTypes = ["implicit", "client_credentials"],
            InitiateLoginUri = Url.Page("/OidcLogin", null, null, Request.Scheme),
            RedirectUris = [launchUrl],
            ClientName = "LtiAdvantage sample tool",
            ClientUri = Url.Page("/Index", null, null, Request.Scheme),
            JwksUri = Url.RouteUrl(Constants.ServiceEndpoints.Jwks.JwksService, null, Request.Scheme),
            TokenEndpointAuthMethod = "private_key_jwt",
            Scope = string.Join(" ", scopes),
            LtiToolConfiguration = new LtiToolConfiguration
            {
                Domain = Request.Host.Value,
                TargetLinkUri = launchUrl,
                Description = "Sample LTI 1.3 tool built with the LtiAdvantage library.",
                Claims = ["iss", "sub", "name", "given_name", "family_name", "email"],
                Messages = messages,
            },
        };
    }
}
