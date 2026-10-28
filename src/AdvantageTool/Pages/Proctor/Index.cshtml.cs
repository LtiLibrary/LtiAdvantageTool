using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdvantageTool.Services;
using IdentityModel.Client;
using LtiAdvantage;
using LtiAdvantage.Lti;
using LtiAdvantage.Proctoring;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages.Proctor;

/// <summary>
/// The proctor's view: attempts in progress, with buttons that call the platform's
/// Assessment Control Service (Proctoring 1.0 §5) to pause, resume, terminate, flag or
/// extend an attempt. Only the actions the platform listed in the acs claim are offered.
/// </summary>
public class IndexModel(ProctoringSessions sessions, AccessTokenService tokens, IHttpClientFactory httpClientFactory) : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public IReadOnlyList<ProctoringSession> Sessions { get; private set; } = [];
    public string? Message { get; private set; }

    public void OnGet() => Sessions = sessions.All();

    public async Task<IActionResult> OnPostAsync(string sessionId, string control)
    {
        var session = sessions.Get(sessionId);
        if (session?.AssessmentControlUrl is null || !session.Actions.Contains(control)
            || !Enum.TryParse<AssessmentControlAction>(control, ignoreCase: true, out var controlAction))
        {
            Message = "That attempt or action is not available.";
            Sessions = sessions.All();
            return Page();
        }

        var request = new AssessmentControlRequest
        {
            // The user is identified by the platform's issuer and the user's id (sub) there.
            User = new AssessmentControlUser { Iss = session.Issuer, Sub = session.UserId },
            ResourceLink = new ResourceLinkClaimValueType { Id = session.ResourceLinkId },
            AttemptNumber = session.AttemptNumber,
            Action = controlAction,
            IncidentTime = DateTime.UtcNow,
            IncidentSeverity = controlAction is AssessmentControlAction.Terminate ? 1.0 : 0.5,
            ReasonCode = "proctor",
            ReasonMsg = $"The proctor chose {control}.",
            // update grants extra time (in seconds).
            ExtraTime = controlAction is AssessmentControlAction.Update ? 300 : null,
        };

        Message = await SendAsync(session, request);
        session.Log.Add($"{DateTime.UtcNow:HH:mm:ss} {control}: {Message}");
        Sessions = sessions.All();
        return Page();
    }

    private async Task<string> SendAsync(ProctoringSession session, AssessmentControlRequest request)
    {
        var token = await tokens.GetAccessTokenAsync(session.PlatformId, Constants.LtiScopes.Ap.ControlAll);
        if (token.IsError) return $"token error: {token.Error}";

        var http = httpClientFactory.CreateClient();
        http.SetBearerToken(token.AccessToken!);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(Constants.MediaTypes.AssessmentControl));

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, Constants.MediaTypes.AssessmentControl);
            using var response = await http.PostAsync(session.AssessmentControlUrl, content);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return $"{(int)response.StatusCode} {response.ReasonPhrase} {body}";

            // The platform answers with the attempt's new status (running, paused, terminated, ...).
            var result = JsonSerializer.Deserialize<AssessmentControlResponse>(body);
            if (result is not null && result.Status != AssessmentControlStatus.Unknown)
                session.Status = result.Status.ToString().ToLowerInvariant();
            return $"status {session.Status}" + (result?.ExtraTime is { } extra ? $", extra time {extra}s" : "");
        }
        catch (Exception e) { return e.Message; }
    }
}
