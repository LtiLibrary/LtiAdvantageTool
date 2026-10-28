using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AdvantageTool.Data;
using AdvantageTool.Services;
using AdvantageTool.Utility;
using IdentityModel;
using LtiAdvantage;
using LtiAdvantage.IdentityModel.Client;
using LtiAdvantage.Lti;
using LtiAdvantage.Proctoring;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Pages;

/// <summary>
/// Proctoring Services 1.0, the student's side. The platform launches LtiStartProctoring; the
/// tool checks the student's identity (pretend, here) and sends LtiStartAssessment back to the
/// platform's start_assessment_url, which continues into the assessment. When the attempt is
/// over, the platform may launch LtiEndAssessment back to the tool.
/// The proctor's controls (Assessment Control Service) are on /Proctor.
/// </summary>
[IgnoreAntiforgeryToken]
public class ProctoringModel(ApplicationDbContext context, LaunchStore launches, ProctoringSessions sessions) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? LaunchId { get; set; }
    [BindProperty] public string? FullName { get; set; }
    [BindProperty] public bool Confirmed { get; set; }

    public LtiStartProctoringRequest? StartRequest { get; private set; }
    public LtiEndAssessmentRequest? EndRequest { get; private set; }
    public string? Error { get; private set; }

    public IActionResult OnGet()
    {
        var launch = launches.Get(LaunchId);
        switch (launch?.MessageType)
        {
            case Constants.Lti.LtiStartProctoringRequestMessageType:
                StartRequest = new LtiStartProctoringRequest(launch.Payload);
                // name is optional in OpenID Connect; many platforms only send given and family name.
                FullName = StartRequest.Name ?? $"{StartRequest.GivenName} {StartRequest.FamilyName}".Trim();
                break;
            case Constants.Lti.LtiEndAssessmentRequestMessageType:
                EndRequest = new LtiEndAssessmentRequest(launch.Payload);
                var session = sessions.Find(launch.PlatformId, EndRequest.UserId ?? "", EndRequest.ResourceLink?.Id ?? "", EndRequest.AttemptNumber ?? 0);
                if (session is not null)
                {
                    session.Status = string.IsNullOrEmpty(EndRequest.ErrorMessage) ? "ended" : "ended with an error";
                    session.Log.Add($"{DateTime.UtcNow:HH:mm:ss} LtiEndAssessment received {EndRequest.ErrorMessage}");
                }
                break;
            default:
                Error = "This launch has expired. Launch the assessment again from the platform.";
                break;
        }
        return Page();
    }

    /// <summary>The pretend identity check; on success, hand the student back to the platform.</summary>
    public async Task<IActionResult> OnPostAsync()
    {
        var launch = launches.Get(LaunchId);
        if (launch?.MessageType != Constants.Lti.LtiStartProctoringRequestMessageType)
        {
            Error = "This launch has expired. Launch the assessment again from the platform.";
            return Page();
        }
        StartRequest = new LtiStartProctoringRequest(launch.Payload);

        if (!Confirmed || string.IsNullOrWhiteSpace(FullName))
        {
            Error = "Type your full name and confirm that you are the person taking this assessment.";
            return Page();
        }
        if (string.IsNullOrEmpty(StartRequest.StartAssessmentUrl) || StartRequest.ResourceLink?.Id is null || StartRequest.AttemptNumber is null)
        {
            Error = "The platform did not send start_assessment_url, resource_link and attempt_number.";
            return Page();
        }

        var platform = await context.GetPlatformByPlatformIdAsync(launch.PlatformId);

        // Remember the attempt so the proctor can control it through the Assessment Control Service.
        sessions.Add(new ProctoringSession
        {
            PlatformId = platform!.PlatformId,
            Issuer = platform.Issuer,
            UserId = StartRequest.UserId!,
            UserName = FullName,
            ResourceLinkId = StartRequest.ResourceLink.Id,
            ResourceLinkTitle = StartRequest.ResourceLink.Title,
            AttemptNumber = StartRequest.AttemptNumber.Value,
            AssessmentControlUrl = StartRequest.Acs?.AssessmentControlUrl,
            Actions = StartRequest.Acs?.ActionNames ?? [],
        });

        // Proctoring 1.0 §4.3: the tool's reply. session_data must come back unchanged; it lets the
        // platform tie this message to the attempt it started.
        var start = new LtiStartAssessmentRequest
        {
            DeploymentId = StartRequest.DeploymentId,
            ResourceLink = new ResourceLinkClaimValueType { Id = StartRequest.ResourceLink.Id },
            AttemptNumber = StartRequest.AttemptNumber,
            SessionData = StartRequest.SessionData,
            ProctoringSettings = StartRequest.ProctoringSettings,
            // The identity the tool verified, so the platform can show or record it.
            VerifiedUser = new VerifiedUserClaimValueType { Name = FullName },
            // Ask the platform to send LtiEndAssessment back when the attempt is over.
            EndAssessmentReturn = true,
        };

        var now = DateTime.UtcNow;
        start.AddClaim(new Claim(JwtRegisteredClaimNames.Iss, platform.ClientId));
        start.AddClaim(new Claim(JwtRegisteredClaimNames.Aud, platform.Issuer));
        start.AddClaim(new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer64));
        start.AddClaim(new Claim(JwtRegisteredClaimNames.Exp, EpochTime.GetIntDate(now.AddMinutes(5)).ToString(), ClaimValueTypes.Integer64));
        start.AddClaim(new Claim(JwtRegisteredClaimNames.Nonce, CryptoRandom.CreateUniqueId()));

        var credentials = RsaPemKeys.SigningCredentialsFromPem(platform.PrivateKey, platform.KeyId);
        var jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credentials), start));
        return AutoPost.Form(StartRequest.StartAssessmentUrl, new Dictionary<string, string> { ["JWT"] = jwt });
    }
}
