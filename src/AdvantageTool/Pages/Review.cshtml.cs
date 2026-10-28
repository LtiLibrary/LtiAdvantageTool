using AdvantageTool.Services;
using LtiAdvantage;
using LtiAdvantage.AssignmentGradeServices;
using LtiAdvantage.IdentityModel.Client;
using LtiAdvantage.Lti;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages;

/// <summary>
/// Submission Review 1.0: the platform launches back into the tool from its gradebook to show
/// what one user submitted. The user under review is in the for_user claim; the line item is in
/// the AGS claim. For this sample, "the submission" is the result the tool posted for that user.
/// </summary>
public class ReviewModel(LaunchStore launches, AccessTokenService tokens, IHttpClientFactory httpClientFactory) : PageModel
{
    public LtiSubmissionReviewRequest? LtiRequest { get; private set; }
    public LineItem? LineItem { get; private set; }
    public IList<Result> Results { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? launchId)
    {
        var launch = launches.Get(launchId);
        if (launch is null || launch.MessageType != Constants.Lti.LtiSubmissionReviewRequestMessageType)
        {
            Error = "This launch has expired. Launch the review again from the platform.";
            return Page();
        }
        LtiRequest = new LtiSubmissionReviewRequest(launch.Payload);

        var userId = LtiRequest.ForUser?.UserId;
        var lineItemUrl = LtiRequest.AssignmentGradeServices?.LineItemUrl;
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(lineItemUrl))
        {
            Error = "A submission review launch needs a for_user claim and the AGS lineitem.";
            return Page();
        }

        var token = await tokens.GetAccessTokenAsync(launch.PlatformId,
            $"{Constants.LtiScopes.Ags.LineItemReadonly} {Constants.LtiScopes.Ags.ResultReadonly}");
        if (token.IsError) { Error = token.Error; return Page(); }

        // The library's HttpClient extensions: GetResultsAsync adds /results and the user_id
        // filter and follows paging; GetLineItemAsync reads the line item.
        var http = httpClientFactory.CreateClient();
        try
        {
            // Only the reviewed user's results (AGS 2.0 §3.4 user_id filter).
            Results = await http.GetResultsAsync(token.AccessToken!, lineItemUrl, userId);
        }
        catch (Exception e) { Error = $"Results: {e.Message}"; }

        try
        {
            // The line item itself, for its label; the review still works without it.
            LineItem = await http.GetLineItemAsync(token.AccessToken!, lineItemUrl);
        }
        catch (Exception e) { Error ??= $"Line item: {e.Message}"; }

        return Page();
    }
}
