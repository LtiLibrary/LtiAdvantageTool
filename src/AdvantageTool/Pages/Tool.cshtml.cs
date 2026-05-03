using System.IdentityModel.Tokens.Jwt;
using System.Text;
using AdvantageTool.Data;
using LtiAdvantage;
using LtiAdvantage.Lti;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Pages;

[IgnoreAntiforgeryToken]
public class ToolModel(
    ApplicationDbContext context,
    StateDbContext state,
    IHttpClientFactory httpClientFactory) : PageModel
{
    public string? Error { get; set; }
    public string? IdToken { get; set; }
    public JwtHeader? JwtHeader { get; set; }
    public LtiResourceLinkRequest? LtiRequest { get; set; }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync(
        string platformId,
        [FromForm(Name = "id_token")] string? idToken,
        [FromForm(Name = "state")] string? stateValue)
    {
        if (string.IsNullOrEmpty(idToken)) { Error = "id_token missing"; return Page(); }

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(idToken)) { Error = "Cannot read id_token"; return Page(); }
        var jwt = handler.ReadJwtToken(idToken);
        JwtHeader = jwt.Header;

        var messageType = jwt.Claims.SingleOrDefault(c => c.Type == Constants.LtiClaims.MessageType)?.Value;
        if (string.IsNullOrEmpty(messageType)) { Error = "message_type claim missing"; return Page(); }

        var nonce = jwt.Claims.SingleOrDefault(c => c.Type == "nonce")?.Value;
        if (string.IsNullOrEmpty(nonce)) { Error = "nonce missing"; return Page(); }
        var memorized = state.GetState(nonce);
        if (memorized is null) { Error = "Invalid nonce (possible replay)"; return Page(); }
        if (memorized.Value != stateValue) { Error = "state mismatch"; return Page(); }

        var platform = await context.GetPlatformByPlatformIdAsync(platformId);
        if (platform is null) { Error = "Unknown platform"; return Page(); }

        SecurityKey signingKey;
        try
        {
            var http = httpClientFactory.CreateClient();
            var keySetJson = await http.GetStringAsync(platform.JwkSetUrl);
            var keySet = new JsonWebKeySet(keySetJson);
            var match = keySet.Keys.SingleOrDefault(k => k.Kid == jwt.Header.Kid);
            if (match is null) { Error = $"Platform did not advertise key {jwt.Header.Kid}"; return Page(); }
            signingKey = match;
        }
        catch (Exception e) { Error = e.Message; return Page(); }

        try
        {
            handler.ValidateToken(idToken, new TokenValidationParameters
            {
                ValidIssuer = platform.Issuer,
                ValidAudience = platform.ClientId,
                IssuerSigningKey = signingKey,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5),
            }, out _);
        }
        catch (Exception e) { Error = e.Message; return Page(); }

        if (messageType == Constants.Lti.LtiDeepLinkingRequestMessageType)
            return AutoPost("./Catalog", new { IdToken = idToken });

        IdToken = idToken;
        LtiRequest = new LtiResourceLinkRequest(jwt.Payload);
        return Page();
    }

    private ContentResult AutoPost(string url, object values)
    {
        var dict = values.GetType().GetProperties()
            .ToDictionary(p => p.Name, p => p.GetValue(values)?.ToString() ?? "");
        var sb = new StringBuilder();
        sb.Append("<html><body onload=\"document.forms[0].submit()\"><form method=\"post\" action=\"")
          .Append(url).Append("\">");
        foreach (var (k, v) in dict)
            sb.Append("<input type=\"hidden\" name=\"").Append(k).Append("\" value=\"").Append(System.Net.WebUtility.HtmlEncode(v)).Append("\"/>");
        sb.Append("</form></body></html>");
        return new ContentResult
        {
            Content = sb.ToString(),
            ContentType = "text/html",
            StatusCode = StatusCodes.Status200OK,
        };
    }
}
