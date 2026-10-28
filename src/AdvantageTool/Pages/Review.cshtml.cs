using System.Net.Http.Headers;
using System.Text.Json;
using AdvantageTool.Services;
using AdvantageTool.Utility;
using IdentityModel.Client;
using LtiAdvantage;
using LtiAdvantage.AssignmentGradeServices;
using LtiAdvantage.Lti;
using LtiAdvantage.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace AdvantageTool.Pages;

/// <summary>
/// Submission Review 1.0: the platform launches back into the tool from its gradebook to show
/// what one user submitted. The user under review is in the for_user claim; the line item is in
/// the AGS claim. For this sample, "the submission" is the result the tool posted for that user.
/// </summary>
public class ReviewModel(LaunchStore launches, AccessTokenService tokens, IHttpClientFactory httpClientFactory) : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new IsoDateTimeConverter() },
    };

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

        var http = httpClientFactory.CreateClient();
        http.SetBearerToken(token.AccessToken!);
        try
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, lineItemUrl))
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(Constants.MediaTypes.LineItem));
                using var response = await http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                    LineItem = JsonSerializer.Deserialize<LineItem>(await response.Content.ReadAsStringAsync(), JsonOptions);
            }

            // Only the reviewed user's results (AGS 2.0 §3.4 user_id filter).
            var url = QueryHelpers.AddQueryString(AgsUrls.Results(lineItemUrl), "user_id", userId);
            (Results, _) = await Paging.GetAllPagesAsync(http, url, Constants.MediaTypes.ResultContainer,
                json => JsonSerializer.Deserialize<ResultContainer>(json, JsonOptions) ?? []);
        }
        catch (Exception e) { Error = e.Message; }

        return Page();
    }
}
