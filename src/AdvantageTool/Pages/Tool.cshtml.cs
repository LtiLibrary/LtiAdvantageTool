using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AdvantageTool.Services;
using AdvantageTool.Utility;
using IdentityModel.Client;
using LtiAdvantage;
using LtiAdvantage.AssignmentGradeServices;
using LtiAdvantage.IdentityModel.Client;
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
        [FromForm(Name = "resource_id")] string? resourceId,
        [FromForm] string? gradesReleased)
    {
        if (!LoadLaunch(launchId)) return Page();

        var lineItemsUrl = LtiRequest!.AssignmentGradeServices?.LineItemsUrl;
        if (string.IsNullOrEmpty(lineItemsUrl)) { Error = "AGS not present in launch."; return Page(); }

        var token = await GetTokenAsync(Constants.LtiScopes.Ags.LineItem);
        if (token is null) return Page();

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
            // AGS 2.0: whether the platform may show these grades to learners. Not set means
            // the platform decides.
            GradesReleased = GradesReleased.Parse(gradesReleased),
            // Submission Review 1.0: lets the platform launch back into the tool from the gradebook
            // (LtiSubmissionReviewRequest) to show one user's submission. The launch URL handles it.
            SubmissionReviewExtension = new SubmissionReviewProperty
            {
                Label = "Review in AdvantageTool",
                Url = Url.Page("/Tool", null, new { platformId = Launch!.PlatformId }, Request.Scheme),
            },
        };

        // The library's HttpClient extensions send the token, serialize the line item and turn an
        // error answer into an LtiServiceException with what the platform said.
        await CallAsync("Create line item", http => http.AddLineItemAsync(token, lineItemsUrl, lineItem));
        return Page();
    }

    /// <summary>
    /// Changes a line item's label, scoreMaximum and gradesReleased (AGS 2.0 PUT).
    /// </summary>
    public async Task<IActionResult> OnPostUpdateLineItemAsync(
        [FromForm] string? launchId,
        [FromForm] string? lineItemUrl,
        [FromForm] string? label,
        [FromForm] double? scoreMaximum,
        [FromForm] string? gradesReleased)
    {
        if (!LoadLaunch(launchId)) return Page();
        if (!IsLineItemUrl(lineItemUrl)) return Page();
        if (string.IsNullOrWhiteSpace(label)) { Error = "Update line item failed: a label is required."; return Page(); }
        if (scoreMaximum is not > 0) { Error = "Update line item failed: scoreMaximum must be a number above 0."; return Page(); }

        var token = await GetTokenAsync(Constants.LtiScopes.Ags.LineItem);
        if (token is null) return Page();

        await CallAsync("Update line item", async http =>
        {
            // PUT replaces the whole line item, so start from what the platform has and change
            // only the fields on the form. Otherwise resourceLinkId, tag, dates and
            // submissionReview would be cleared. (Fields the LineItem class doesn't model, such
            // as vendor extensions, are not kept.)
            var lineItem = await http.GetLineItemAsync(token, lineItemUrl!)
                ?? throw new InvalidOperationException("the platform returned no line item.");
            lineItem.Label = label.Trim();
            lineItem.ScoreMaximum = scoreMaximum;
            lineItem.GradesReleased = GradesReleased.Parse(gradesReleased);
            await http.UpdateLineItemAsync(token, lineItemUrl!, lineItem);
        });
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteLineItemAsync(
        [FromForm] string? launchId,
        [FromForm] string? lineItemUrl)
    {
        if (!LoadLaunch(launchId)) return Page();
        if (!IsLineItemUrl(lineItemUrl)) return Page();

        var token = await GetTokenAsync(Constants.LtiScopes.Ags.LineItem);
        if (token is null) return Page();

        await CallAsync("Delete line item", http => http.DeleteLineItemAsync(token, lineItemUrl!));
        return Page();
    }

    public async Task<IActionResult> OnPostPostScoreAsync(
        [FromForm] string? launchId,
        [FromForm] string? lineItemUrl,
        [FromForm] string? userId,
        [FromForm] double? scoreGiven,
        [FromForm] double scoreMaximum,
        [FromForm] ActivityProgress activityProgress,
        [FromForm] GradingProgress gradingProgress,
        [FromForm] string? comment)
    {
        if (!LoadLaunch(launchId)) return Page();
        if (!IsLineItemUrl(lineItemUrl)) return Page();

        // Only an empty field means "progress only"; a value that is not a number is an error,
        // not a reason to drop the score.
        if (scoreGiven is null && !string.IsNullOrWhiteSpace(Request.Form["scoreGiven"]))
        {
            Error = $"Submit score failed: '{Request.Form["scoreGiven"]}' is not a number.";
            return Page();
        }

        var token = await GetTokenAsync(Constants.LtiScopes.Ags.Score);
        if (token is null) return Page();

        // AGS 2.0 §3.3: activityProgress and gradingProgress tell the platform how far the user
        // got and whether the score is final; the platform decides what to show in its gradebook.
        // An empty score sends progress only: no scoreGiven and no scoreMaximum, so the platform
        // does not record a grade of 0.
        var score = new Score
        {
            ActivityProgress = activityProgress,
            GradingProgress = gradingProgress,
            ScoreGiven = scoreGiven,
            ScoreMaximum = scoreGiven is null ? null : scoreMaximum,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment,
            TimeStamp = DateTime.UtcNow,
            UserId = string.IsNullOrEmpty(userId) ? LtiRequest!.UserId! : userId,
        };

        // PostScoreAsync adds /scores to the line item URL's path, keeping its query.
        await CallAsync("Submit score", http => http.PostScoreAsync(token, lineItemUrl!, score));
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

    private async Task<string?> GetTokenAsync(string scope)
    {
        var token = await tokens.GetAccessTokenAsync(Launch!.PlatformId, scope);
        if (token.IsError && token.Error != "Created") { Error = token.Error; return null; }
        return token.AccessToken;
    }

    /// <summary>Runs a service call and shows what went wrong, if anything.</summary>
    private async Task CallAsync(string what, Func<HttpClient, Task> call)
    {
        try { await call(httpClientFactory.CreateClient()); }
        // LtiServiceException carries the platform's ProblemDetails title and detail.
        catch (Exception e) { Error = $"{what} failed: {e.Message}"; }
    }

    private async Task<HttpClient?> CreateAgsClientAsync(string scope, string mediaType)
    {
        var token = await GetTokenAsync(scope);
        if (token is null) return null;

        var http = httpClientFactory.CreateClient();
        http.SetBearerToken(token);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
        return http;
    }

    private PageResult ShowError(string message)
    {
        Error = message;
        return Page();
    }
}
