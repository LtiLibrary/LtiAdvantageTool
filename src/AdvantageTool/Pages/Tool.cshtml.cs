using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AdvantageTool.Services;
using AdvantageTool.Utility;
using IdentityModel.Client;
using LtiAdvantage;
using LtiAdvantage.AssignmentGradeServices;
using LtiAdvantage.Lti;
using LtiAdvantage.SubmissionReview;
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
            Constants.Lti.LtiSubmissionReviewRequestMessageType => RedirectToPage("/Review", new { launchId }),
            Constants.Lti.LtiStartProctoringRequestMessageType => RedirectToPage("/Proctoring", new { launchId }),
            Constants.Lti.LtiEndAssessmentRequestMessageType => RedirectToPage("/Proctoring", new { launchId }),
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
            // Submission Review 1.0: lets the platform launch back into the tool from the gradebook
            // (LtiSubmissionReviewRequest) to show one user's submission. The launch URL handles it.
            SubmissionReviewExtension = new SubmissionReviewProperty
            {
                Label = "Review in AdvantageTool",
                Url = Url.Page("/Tool", null, new { platformId = Launch!.PlatformId }, Request.Scheme),
            },
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(lineItem, JsonOptions), Encoding.UTF8, Constants.MediaTypes.LineItem);
            using var response = await http.PostAsync(lineItemsUrl, content);
            if (!response.IsSuccessStatusCode) Error = $"Create line item failed: {await ServiceError.DescribeAsync(response)}";
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
            if (!response.IsSuccessStatusCode) Error = $"Delete line item failed: {await ServiceError.DescribeAsync(response)}";
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
            if (!response.IsSuccessStatusCode) Error = $"Submit score failed: {await ServiceError.DescribeAsync(response)}";
        }
        catch (Exception e) { Error = e.Message; }

        return Page();
    }

    /// <summary>
    /// Makes a service call fail on purpose, to show what the platform answers (a ProblemDetails
    /// body from LtiAdvantage platforms) and how the tool reports it.
    /// </summary>
    public async Task<IActionResult> OnPostErrorDemoAsync([FromForm] string? launchId, [FromForm] string? kind)
    {
        if (!LoadLaunch(launchId)) return Page();
        var lineItemsUrl = LtiRequest!.AssignmentGradeServices?.LineItemsUrl;
        if (string.IsNullOrEmpty(lineItemsUrl)) { Error = "AGS not present in launch."; return Page(); }

        try
        {
            if (kind == "missing-line-item")
            {
                // A line item id that does not exist: expect 404.
                var http = await CreateAgsClientAsync(Constants.LtiScopes.Ags.LineItemReadonly, Constants.MediaTypes.LineItem);
                if (http is null) return Page();
                var uri = new Uri(lineItemsUrl);
                using var response = await http.GetAsync($"{uri.GetLeftPart(UriPartial.Path).TrimEnd('/')}/does-not-exist{uri.Query}");
                Error = $"Read a missing line item: {await DescribeExpectedFailureAsync(response)}";
            }
            else
            {
                // Create a line item with a token that only has the read-only scope: expect 403.
                var http = await CreateAgsClientAsync(Constants.LtiScopes.Ags.LineItemReadonly, Constants.MediaTypes.LineItem);
                if (http is null) return Page();
                var content = new StringContent(JsonSerializer.Serialize(new LineItem { Label = "Should fail", ScoreMaximum = 1 }, JsonOptions),
                    Encoding.UTF8, Constants.MediaTypes.LineItem);
                using var response = await http.PostAsync(lineItemsUrl, content);
                Error = $"Create a line item with a read-only token: {await DescribeExpectedFailureAsync(response)}";
            }
        }
        catch (Exception e) { Error = e.Message; }

        return Page();
    }

    private static async Task<string> DescribeExpectedFailureAsync(HttpResponseMessage response) =>
        response.IsSuccessStatusCode
            ? $"the platform accepted the call ({(int)response.StatusCode}), so it did not fail as expected."
            : await ServiceError.DescribeAsync(response);

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
