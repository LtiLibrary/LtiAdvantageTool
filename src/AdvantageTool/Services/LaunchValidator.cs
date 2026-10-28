using System.IdentityModel.Tokens.Jwt;
using AdvantageTool.Data;
using LtiAdvantage.IdentityModel.Validation;
using LtiAdvantage.Lti;
using Microsoft.AspNetCore.WebUtilities;

namespace AdvantageTool.Services;

/// <summary>The outcome of a launch: either a stored <see cref="Launch"/>, or an error to show or send back.</summary>
public sealed record LaunchOutcome(Launch? Launch, string? Error, string? ErrorReturnUrl);

/// <summary>
/// Runs every check the LTI 1.3 Security Framework asks of a tool when a launch arrives.
/// </summary>
public class LaunchValidator(
    ApplicationDbContext context,
    JwksCache jwksCache,
    INonceStore nonceStore,
    LaunchStore launches,
    IHostEnvironment env,
    ILogger<LaunchValidator> logger)
{
    public async Task<LaunchOutcome> ValidateAsync(HttpContext http, string platformId, string? idToken, string? state)
    {
        var platform = await context.GetPlatformByPlatformIdAsync(platformId);
        if (platform is null) return Fail(null, idToken, $"No platform is registered with id '{platformId}'.");

        // The state must come back in the browser that started the login (CSRF protection).
        var expectedNonce = LtiStateCookie.Take(http, state);
        if (expectedNonce is null)
            return Fail(platform, idToken, "The state does not match a login started in this browser.");

        // LtiTokenValidator checks the signature against the platform's JWKS (cached, refreshed
        // when the platform rotates its key), iss, aud/azp, exp/iat, the LTI version,
        // message_type, that deployment_id is one registered for this platform, that the nonce
        // is the one this tool sent for this state (so a valid token captured from another login
        // cannot be injected here), and that the nonce has not been used before.
        var validator = new LtiTokenValidator(new LtiTokenValidationOptions
        {
            Issuer = platform.Issuer,
            ClientId = platform.ClientId,
            JwksUri = platform.JwkSetUrl,
            NonceStore = nonceStore,
            DeploymentIds = platform.DeploymentIdList.ToList(),
        }, jwksCache);

        var result = await validator.ValidateAsync(idToken!, expectedNonce, http.RequestAborted);
        if (!result.IsValid)
        {
            logger.LogWarning(result.Exception, "Launch rejected: {Error}", result.Error);
            return Fail(platform, idToken, result.Error);
        }

        var header = new JwtSecurityTokenHandler().ReadJwtToken(idToken).Header;
        return new LaunchOutcome(launches.Save(platform.PlatformId, result.MessageType, header, result.Payload), null, null);
    }

    private LaunchOutcome Fail(Platform? platform, string? idToken, string reason)
    {
        var traceId = System.Diagnostics.Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        logger.LogWarning("Launch rejected ({TraceId}): {Reason}", traceId, reason);

        // The details help an attacker probing the endpoint, so only show them while developing.
        var message = env.IsDevelopment()
            ? $"The launch could not be verified: {reason}"
            : "The launch could not be verified.";
        var log = $"LTI launch rejected by the tool. Trace id: {traceId}";

        return new LaunchOutcome(null, message, ErrorReturnUrl(platform, idToken, message, log));
    }

    /// <summary>
    /// LTI 1.3 §4.4.1: when the platform sent launch_presentation.return_url, send the user
    /// back there with lti_errormsg / lti_errorlog instead of leaving them on a tool error page.
    /// </summary>
    private static string? ErrorReturnUrl(Platform? platform, string? idToken, string message, string log)
    {
        if (platform is null || string.IsNullOrEmpty(idToken)) return null;

        string? returnUrl;
        try
        {
            var payload = new JwtSecurityTokenHandler().ReadJwtToken(idToken).Payload;
            returnUrl = new LtiResourceLinkRequest(payload).LaunchPresentation?.ReturnUrl;
        }
        catch (Exception) { return null; }

        // The token failed validation, so its return_url is untrusted: only follow it back to
        // the registered platform's own origin, or this would be an open redirect.
        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var target)) return null;
        var platformOrigins = new[] { platform.Issuer, platform.AuthorizeUrl }
            .Select(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : null);
        if (!platformOrigins.Contains(target.GetLeftPart(UriPartial.Authority))) return null;

        return QueryHelpers.AddQueryString(returnUrl!, new Dictionary<string, string?>
        {
            ["lti_errormsg"] = message,
            ["lti_errorlog"] = log,
        });
    }
}
