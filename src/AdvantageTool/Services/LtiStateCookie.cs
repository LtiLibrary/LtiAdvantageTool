namespace AdvantageTool.Services;

/// <summary>
/// Binds the OIDC login to the browser that started it (LTI 1.3 Security Framework §5.1.1.2:
/// the tool must verify the state it sent comes back in the same user agent). The cookie
/// name carries the state, so several launches in different frames don't overwrite each other.
/// The value is the nonce, so the launch can also check the id_token was minted for this login.
/// </summary>
public static class LtiStateCookie
{
    private const string Prefix = "lti1p3-state-";

    public static void Set(HttpResponse response, string state, string nonce)
    {
        var options = Options();
        options.MaxAge = TimeSpan.FromMinutes(5);
        response.Cookies.Append(Prefix + state, nonce, options);
    }

    private static CookieOptions Options()
    {
        var options = new CookieOptions
        {
            // The launch is a cross-site form post from the platform, usually inside an iframe,
            // so the cookie must be SameSite=None (which in turn requires Secure).
            SameSite = SameSiteMode.None,
            Secure = true,
            HttpOnly = true,
            IsEssential = true,
        };
        // CHIPS: a partitioned cookie still works in an iframe when third-party cookies are blocked,
        // because login initiation and launch both happen under the same top-level platform site.
        options.Extensions.Add("Partitioned");
        return options;
    }

    /// <summary>Returns the nonce stored for this state, and removes the cookie (single use).</summary>
    public static string? Take(HttpContext http, string? state)
    {
        if (string.IsNullOrEmpty(state)) return null;
        var name = Prefix + state;
        if (!http.Request.Cookies.TryGetValue(name, out var nonce)) return null;
        http.Response.Cookies.Delete(name, Options());
        return nonce;
    }
}
