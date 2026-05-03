using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AdvantageTool.Data;
using AdvantageTool.Services;
using IdentityModel.Client;
using LtiAdvantage;
using LtiAdvantage.AssignmentGradeServices;
using LtiAdvantage.Lti;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Pages;

[IgnoreAntiforgeryToken]
public class ToolModel(
    ApplicationDbContext context,
    StateDbContext state,
    IHttpClientFactory httpClientFactory,
    AccessTokenService tokens) : PageModel
{
    public string? Error { get; set; }
    public string? IdToken { get; set; }
    public string? PlatformId { get; set; }
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
            return AutoPost(Url.Page("/Catalog")!, new { IdToken = idToken });

        IdToken = idToken;
        PlatformId = platformId;
        LtiRequest = new LtiResourceLinkRequest(jwt.Payload);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateLineItemAsync(
        [FromForm(Name = "id_token")] string? idToken,
        [FromForm(Name = "resource_link_id")] string? resourceLinkId)
    {
        if (!RestoreLaunchState(idToken)) return Page();

        var lineItemsUrl = LtiRequest!.AssignmentGradeServices?.LineItemsUrl;
        if (string.IsNullOrEmpty(lineItemsUrl)) { Error = "AGS not present in launch."; return Page(); }

        var http = await CreateAgsClientAsync(Constants.LtiScopes.Ags.LineItem, Constants.MediaTypes.LineItem);
        if (http is null) return Page();

        var lineItem = new LineItem
        {
            EndDateTime = DateTime.UtcNow.AddMonths(3),
            Label = LtiRequest.ResourceLink?.Title ?? "Line item",
            ResourceLinkId = string.IsNullOrEmpty(resourceLinkId) ? LtiRequest.ResourceLink?.Id : resourceLinkId,
            ScoreMaximum = 100,
            StartDateTime = DateTime.UtcNow,
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(lineItem), Encoding.UTF8, Constants.MediaTypes.LineItem);
            using var response = await http.PostAsync(lineItemsUrl, content);
            if (!response.IsSuccessStatusCode) Error = $"Create line item failed: {(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (Exception e) { Error = e.Message; }

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteLineItemAsync(
        [FromForm(Name = "id_token")] string? idToken,
        [FromForm(Name = "lineItemUrl")] string? lineItemUrl)
    {
        if (!RestoreLaunchState(idToken)) return Page();
        if (string.IsNullOrEmpty(lineItemUrl)) { Error = "lineItemUrl missing"; return Page(); }

        var http = await CreateAgsClientAsync(Constants.LtiScopes.Ags.LineItem, Constants.MediaTypes.LineItem);
        if (http is null) return Page();

        try
        {
            using var response = await http.DeleteAsync(lineItemUrl);
            if (!response.IsSuccessStatusCode) Error = $"Delete line item failed: {(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (Exception e) { Error = e.Message; }

        return Page();
    }

    public async Task<IActionResult> OnPostPostScoreAsync(
        [FromForm(Name = "id_token")] string? idToken,
        [FromForm(Name = "lineItemUrl")] string? lineItemUrl)
    {
        if (!RestoreLaunchState(idToken)) return Page();
        if (string.IsNullOrEmpty(lineItemUrl)) { Error = "lineItemUrl missing"; return Page(); }

        var http = await CreateAgsClientAsync(Constants.LtiScopes.Ags.Score, Constants.MediaTypes.Score);
        if (http is null) return Page();

        var score = new Score
        {
            ActivityProgress = ActivityProgress.Completed,
            GradingProgress = GradingProgress.FullyGraded,
            ScoreGiven = Math.Round(Random.Shared.NextDouble() * 100, 2),
            ScoreMaximum = 100,
            TimeStamp = DateTime.UtcNow,
            UserId = LtiRequest!.UserId!,
        };
        if (score.ScoreGiven > 75) score.Comment = "Good job!";

        try
        {
            var scoresUrl = $"{lineItemUrl.TrimEnd('/')}/scores";
            var content = new StringContent(JsonSerializer.Serialize(score), Encoding.UTF8, Constants.MediaTypes.Score);
            using var response = await http.PostAsync(scoresUrl, content);
            if (!response.IsSuccessStatusCode) Error = $"Submit score failed: {(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (Exception e) { Error = e.Message; }

        return Page();
    }

    private bool RestoreLaunchState(string? idToken)
    {
        if (string.IsNullOrEmpty(idToken)) { Error = "id_token missing"; return false; }
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(idToken)) { Error = "Cannot read id_token"; return false; }
        var jwt = handler.ReadJwtToken(idToken);
        IdToken = idToken;
        PlatformId = RouteData.Values["platformId"]?.ToString();
        JwtHeader = jwt.Header;
        LtiRequest = new LtiResourceLinkRequest(jwt.Payload);
        return true;
    }

    private async Task<HttpClient?> CreateAgsClientAsync(string scope, string mediaType)
    {
        var token = await tokens.GetAccessTokenAsync(LtiRequest!.Iss!, scope);
        if (token.IsError && token.Error != "Created") { Error = token.Error; return null; }

        var http = httpClientFactory.CreateClient();
        http.SetBearerToken(token.AccessToken!);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
        return http;
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
