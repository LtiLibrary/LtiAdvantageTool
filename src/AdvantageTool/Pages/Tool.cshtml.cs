using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AdvantageTool.Services;
using AdvantageTool.Utility;
using IdentityModel.Client;
using LtiAdvantage;
using LtiAdvantage.AssignmentGradeServices;
using LtiAdvantage.Lti;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages;

[IgnoreAntiforgeryToken]
public class ToolModel(
    LaunchValidator validator,
    LaunchStore launches,
    IHttpClientFactory httpClientFactory,
    AccessTokenService tokens) : PageModel
{
    // Leave unset properties out instead of sending explicit nulls to the platform.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public string? Error { get; set; }
    public Launch? Launch { get; private set; }
    public LtiResourceLinkRequest? LtiRequest { get; private set; }

    /// <summary>
    /// The platform posts the id_token here (the launch URL / redirect_uri).
    /// </summary>
    public async Task<IActionResult> OnPostAsync(
        string platformId,
        [FromForm(Name = "id_token")] string? idToken,
        [FromForm(Name = "state")] string? state)
    {
        var outcome = await validator.ValidateAsync(HttpContext, platformId, idToken, state);
        if (outcome.Launch is null)
        {
            if (outcome.ErrorReturnUrl is not null) return Redirect(outcome.ErrorReturnUrl);
            Error = outcome.Error;
            return Page();
        }

        // Post/Redirect/Get: the validated launch is kept server-side and the browser only
        // carries its id, so a refresh does not resubmit (and fail) the single-use id_token.
        var launchId = outcome.Launch.Id;
        return outcome.Launch.MessageType switch
        {
            Constants.Lti.LtiResourceLinkRequestMessageType => RedirectToPage("/Tool", new { platformId, launchId }),
            Constants.Lti.LtiDeepLinkingRequestMessageType => RedirectToPage("/Catalog", new { launchId }),
            _ => ShowError($"This tool does not handle {outcome.Launch.MessageType} messages."),
        };
    }

    public IActionResult OnGet(string? launchId)
    {
        LoadLaunch(launchId);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateLineItemAsync(
        [FromForm] string? launchId,
        [FromForm(Name = "resource_link_id")] string? resourceLinkId,
        [FromForm] string? tag,
        [FromForm(Name = "resource_id")] string? resourceId)
    {
        if (!LoadLaunch(launchId)) return Page();

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
            // tag and resourceId are the tool's own labels; the line item filters match on them.
            Tag = string.IsNullOrWhiteSpace(tag) ? null : tag,
            ResourceId = string.IsNullOrWhiteSpace(resourceId) ? null : resourceId,
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(lineItem, JsonOptions), Encoding.UTF8, Constants.MediaTypes.LineItem);
            using var response = await http.PostAsync(lineItemsUrl, content);
            if (!response.IsSuccessStatusCode) Error = $"Create line item failed: {(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (Exception e) { Error = e.Message; }

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteLineItemAsync(
        [FromForm] string? launchId,
        [FromForm] string? lineItemUrl)
    {
        if (!LoadLaunch(launchId)) return Page();
        if (!IsLineItemUrl(lineItemUrl)) return Page();

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
        [FromForm] string? launchId,
        [FromForm] string? lineItemUrl,
        [FromForm] string? userId,
        [FromForm] double scoreGiven,
        [FromForm] double scoreMaximum,
        [FromForm] ActivityProgress activityProgress,
        [FromForm] GradingProgress gradingProgress,
        [FromForm] string? comment)
    {
        if (!LoadLaunch(launchId)) return Page();
        if (!IsLineItemUrl(lineItemUrl)) return Page();

        var http = await CreateAgsClientAsync(Constants.LtiScopes.Ags.Score, Constants.MediaTypes.Score);
        if (http is null) return Page();

        // AGS 2.0 §3.3: activityProgress and gradingProgress tell the platform how far the user
        // got and whether the score is final; the platform decides what to show in its gradebook.
        var score = new Score
        {
            ActivityProgress = activityProgress,
            GradingProgress = gradingProgress,
            ScoreGiven = scoreGiven,
            ScoreMaximum = scoreMaximum,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment,
            TimeStamp = DateTime.UtcNow,
            UserId = string.IsNullOrEmpty(userId) ? LtiRequest!.UserId! : userId,
        };

        try
        {
            var scoresUrl = AgsUrls.Scores(lineItemUrl!);
            var content = new StringContent(JsonSerializer.Serialize(score, JsonOptions), Encoding.UTF8, Constants.MediaTypes.Score);
            using var response = await http.PostAsync(scoresUrl, content);
            if (!response.IsSuccessStatusCode) Error = $"Submit score failed: {(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (Exception e) { Error = e.Message; }

        return Page();
    }

    private bool LoadLaunch(string? launchId)
    {
        Launch = launches.Get(launchId);
        if (Launch is null) { Error = "This launch has expired. Launch the tool again from the platform."; return false; }
        LtiRequest = new LtiResourceLinkRequest(Launch.Payload);
        return true;
    }

    /// <summary>
    /// The tool sends its access token to this URL, so only accept line items the platform
    /// advertised in this launch's AGS claim (same origin), never an arbitrary posted URL.
    /// </summary>
    private bool IsLineItemUrl(string? lineItemUrl)
    {
        if (AgsUrls.IsSameOrigin(lineItemUrl, LtiRequest!.AssignmentGradeServices?.LineItemsUrl)) return true;
        Error = "That line item does not belong to this platform.";
        return false;
    }

    private async Task<HttpClient?> CreateAgsClientAsync(string scope, string mediaType)
    {
        var token = await tokens.GetAccessTokenAsync(Launch!.PlatformId, scope);
        if (token.IsError && token.Error != "Created") { Error = token.Error; return null; }

        var http = httpClientFactory.CreateClient();
        http.SetBearerToken(token.AccessToken!);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
        return http;
    }

    private PageResult ShowError(string message)
    {
        Error = message;
        return Page();
    }
}
