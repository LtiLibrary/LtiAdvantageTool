using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AdvantageTool.Data;
using AdvantageTool.Services;
using AdvantageTool.Utility;
using LtiAdvantage.DeepLinking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Pages;

[IgnoreAntiforgeryToken]
public class CatalogModel(ApplicationDbContext context, LaunchStore launches) : PageModel
{
    private static readonly string[] SampleActivities =
    {
        "Reading comprehension drill",
        "Algebra warm-up",
        "World history quiz: 18th century",
        "Lab safety video",
        "Vocabulary builder",
        "Geometry challenge",
    };

    [BindProperty(SupportsGet = true)] public string? LaunchId { get; set; }
    [BindProperty] public IList<Activity> Activities { get; set; } = [];

    public LtiDeepLinkingRequest? LtiRequest { get; private set; }
    public string? Error { get; private set; }

    public IActionResult OnGet()
    {
        var launch = launches.Get(LaunchId);
        if (launch is null) { Error = "This launch has expired. Launch the tool again from the platform."; return Page(); }
        LtiRequest = new LtiDeepLinkingRequest(launch.Payload);
        Activities = SampleActivities
            .Select((title, i) => new Activity { Id = i, Title = title, Description = $"Sample LTI activity: {title}" })
            .ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAssignActivitiesAsync()
    {
        var launch = launches.Get(LaunchId);
        if (launch is null) { Error = "This launch has expired. Launch the tool again from the platform."; return Page(); }
        LtiRequest = new LtiDeepLinkingRequest(launch.Payload);

        var response = new LtiDeepLinkingResponse
        {
            Data = LtiRequest.DeepLinkingSettings.Data,
            DeploymentId = LtiRequest.DeploymentId,
        };

        var platform = await context.GetPlatformByPlatformIdAsync(launch.PlatformId);

        var contentItems = Activities
            .Where(a => a.Selected)
            .Select(a => (ContentItem)new LtiLinkItem
            {
                Title = a.Title,
                Text = a.Description,
                Url = Url.Page("/Tool", null, new { platformId = platform!.PlatformId }, Request.Scheme),
                Custom = new Dictionary<string, string> { ["activity_id"] = a.Id.ToString() },
            })
            .ToArray();

        response.ContentItems = contentItems;
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Iss, LtiRequest.Aud[0]));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Aud, LtiRequest.Iss));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, LtiRequest.Sub));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(DateTime.UtcNow).ToString()));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Nbf, EpochTime.GetIntDate(DateTime.UtcNow.AddSeconds(-5)).ToString()));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Exp, EpochTime.GetIntDate(DateTime.UtcNow.AddMinutes(5)).ToString()));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Nonce, Guid.NewGuid().ToString("N")));

        var creds = PemHelper.SigningCredentialsFromPem(platform!.PrivateKey, platform.KeyId);
        var jwtOut = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(creds), response));

        return AutoPost("JWT", jwtOut, LtiRequest.DeepLinkingSettings.DeepLinkReturnUrl);
    }

    private static ContentResult AutoPost(string name, string value, string url)
    {
        var html = new StringBuilder();
        html.Append("<html><body onload=\"document.forms[0].submit()\"><form method=\"post\" action=\"")
            .Append(url).Append("\"><input type=\"hidden\" name=\"").Append(name)
            .Append("\" value=\"").Append(System.Net.WebUtility.HtmlEncode(value)).Append("\"/></form></body></html>");
        return new ContentResult
        {
            Content = html.ToString(),
            ContentType = "text/html",
            StatusCode = StatusCodes.Status200OK,
        };
    }

    public class Activity
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }
}
