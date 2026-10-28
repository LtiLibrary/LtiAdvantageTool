using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AdvantageTool.Data;
using AdvantageTool.Services;
using AdvantageTool.Utility;
using IdentityModel;
using LtiAdvantage;
using LtiAdvantage.DeepLinking;
using LtiAdvantage.Lti;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Pages;

/// <summary>
/// Deep Linking 2.0: the platform asks the tool for content, the user picks items, and the
/// tool posts a signed LtiDeepLinkingResponse back to deep_link_return_url.
/// </summary>
[IgnoreAntiforgeryToken]
public class CatalogModel(ApplicationDbContext context, LaunchStore launches) : PageModel
{
    /// <summary>What the tool offers. Posted forms only carry the key, never titles or URLs.</summary>
    private static readonly CatalogEntry[] Entries =
    [
        new("reading", Constants.ContentItemTypes.LtiLink, "Reading comprehension drill", "Short texts with questions."),
        new("algebra", Constants.ContentItemTypes.LtiLink, "Algebra warm-up", "Ten quick equations."),
        new("history", Constants.ContentItemTypes.LtiLink, "World history quiz: 18th century", "Multiple choice quiz."),
        new("lab-safety", Constants.ContentItemTypes.LtiLink, "Lab safety video", "Watch, then confirm you understood."),
        new("spec", Constants.ContentItemTypes.Link, "LTI 1.3 specification", "A plain link to a web page."),
        new("banner", Constants.ContentItemTypes.Html, "Welcome banner", "An HTML fragment the platform embeds."),
        new("worksheet", Constants.ContentItemTypes.File, "Worksheet", "A file to download."),
        new("logo", Constants.ContentItemTypes.Image, "Sample image", "An image the platform shows inline."),
    ];

    [BindProperty(SupportsGet = true)] public string? LaunchId { get; set; }
    [BindProperty] public List<Selection> Items { get; set; } = [];
    [BindProperty] public string? Target { get; set; }

    public LtiDeepLinkingRequest? LtiRequest { get; private set; }
    public DeepLinkingSettingsClaimValueType? Settings => LtiRequest?.DeepLinkingSettings;
    public IReadOnlyList<CatalogEntry> Offered { get; private set; } = [];
    public IReadOnlyList<string> Targets { get; private set; } = [];
    public string? Error { get; private set; }

    public IActionResult OnGet()
    {
        if (!Load()) return Page();
        // Nothing is pre-selected: the user chooses, and the page says so if they choose nothing.
        Items = Offered.Select(e => new Selection { Key = e.Key }).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        if (!Load()) return Page();

        var chosen = Items.Where(i => i.Selected).ToList();
        if (chosen.Count == 0)
        {
            Error = "Nothing is selected. Select at least one item, or press Cancel to return without content.";
            return Page();
        }
        // accept_multiple = false means the platform takes at most one item.
        if (chosen.Count > 1 && !Settings!.AcceptMultiple)
        {
            Error = "This platform accepts only one item. Select one.";
            return Page();
        }

        var platform = await context.GetPlatformByPlatformIdAsync(Launch!.PlatformId);
        var items = chosen.Select(s => ToContentItem(Offered.Single(e => e.Key == s.Key), s, platform!)).ToArray();

        var response = NewResponse();
        response.ContentItems = items;
        // msg is shown to the user by the platform, log goes to its log (DL 2.0 §4.4).
        response.Message = $"Added {items.Length} item(s) from the sample tool.";
        response.Log = $"AdvantageTool returned {string.Join(", ", items.Select(i => i.Type))}.";
        return PostBack(response, platform!);
    }

    public async Task<IActionResult> OnPostCancelAsync()
    {
        if (!Load()) return Page();
        var platform = await context.GetPlatformByPlatformIdAsync(Launch!.PlatformId);

        // Returning no items is how a tool says "nothing chosen"; errormsg/errorlog say why.
        var response = NewResponse();
        response.ErrorMessage = "No content was added: the selection was cancelled.";
        response.ErrorLog = "AdvantageTool: user cancelled the deep linking request.";
        return PostBack(response, platform!);
    }

    private Launch? Launch { get; set; }

    private bool Load()
    {
        Launch = launches.Get(LaunchId);
        if (Launch is null || Launch.MessageType != Constants.Lti.LtiDeepLinkingRequestMessageType)
        {
            Error = "This launch has expired. Launch the tool again from the platform.";
            return false;
        }
        LtiRequest = new LtiDeepLinkingRequest(Launch.Payload);

        // Only offer the item types the platform said it accepts (accept_types is required).
        var accepted = Settings?.AcceptTypes ?? [Constants.ContentItemTypes.LtiLink];
        Offered = Entries.Where(e => accepted.Contains(e.Type)).ToList();

        Targets = (Settings?.AcceptPresentationDocumentTargets ?? [])
            .Where(t => t is DocumentTarget.Iframe or DocumentTarget.Window)
            .Select(t => t.ToString().ToLowerInvariant())
            .ToList();
        return true;
    }

    private ContentItem ToContentItem(CatalogEntry entry, Selection selection, Platform platform)
    {
        string Absolute(string path) => $"{Request.Scheme}://{Request.Host}{path}";

        ContentItem item = entry.Type switch
        {
            Constants.ContentItemTypes.LtiLink => new LtiLinkItem
            {
                Url = Url.Page("/Tool", null, new { platformId = platform.PlatformId }, Request.Scheme),
                // activity_id is a plain value; the rest ask the platform for substitutions.
                Custom = new Dictionary<string, string>(CustomVariables.ForLink()) { ["activity_id"] = entry.Key },
                // A lineItem asks the platform to create a gradebook column for this link.
                LineItem = selection.Graded
                    ? new LineItemProperty
                    {
                        Label = entry.Title,
                        ScoreMaximum = selection.ScoreMaximum,
                        Tag = string.IsNullOrWhiteSpace(selection.Tag) ? null : selection.Tag,
                        ResourceId = entry.Key,
                    }
                    : null,
            },
            Constants.ContentItemTypes.Link => new LinkItem { Url = "https://www.imsglobal.org/spec/lti/v1p3" },
            Constants.ContentItemTypes.Html => new HtmlItem
            {
                Html = "<div style=\"padding:1em;background:#eef\"><strong>Welcome!</strong> This block came from the sample tool.</div>",
            },
            Constants.ContentItemTypes.File => new FileItem { Url = Absolute("/files/worksheet.txt") },
            // Width/height are left out on purpose: see the README (library serialization issue).
            Constants.ContentItemTypes.Image => new ImageItem { Url = Absolute("/images/sample.svg") },
            _ => throw new InvalidOperationException($"Unknown item type {entry.Type}"),
        };
        item.Title = entry.Title;
        item.Text = entry.Description;

        // Presentation: only for item types that are opened rather than embedded.
        if (entry.Type is Constants.ContentItemTypes.LtiLink or Constants.ContentItemTypes.Link && Targets.Contains(Target))
        {
            if (Target == "iframe") item.Iframe = new IframeProperty { Src = entry.Type == Constants.ContentItemTypes.Link ? item.Url : null, Height = 600 };
            if (Target == "window") item.Window = new WindowProperty { TargetName = "_blank" };
        }
        return item;
    }

    private LtiDeepLinkingResponse NewResponse() => new()
    {
        // data must be echoed back unchanged (DL 2.0 §4.4.5).
        Data = Settings!.Data,
        DeploymentId = LtiRequest!.DeploymentId,
    };

    /// <summary>Signs the response with the tool's key for this platform and auto-posts it as JWT.</summary>
    private ContentResult PostBack(LtiDeepLinkingResponse response, Platform platform)
    {
        var now = DateTime.UtcNow;
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Iss, platform.ClientId));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Aud, platform.Issuer));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer64));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Exp, EpochTime.GetIntDate(now.AddMinutes(5)).ToString(), ClaimValueTypes.Integer64));
        response.AddClaim(new Claim(JwtRegisteredClaimNames.Nonce, CryptoRandom.CreateUniqueId()));

        var credentials = PemHelper.SigningCredentialsFromPem(platform.PrivateKey, platform.KeyId);
        var jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credentials), response));
        return AutoPost.Form(Settings!.DeepLinkReturnUrl, new Dictionary<string, string> { ["JWT"] = jwt });
    }

    public record CatalogEntry(string Key, string Type, string Title, string Description);

    public class Selection
    {
        public string Key { get; set; } = string.Empty;
        public bool Selected { get; set; }
        public bool Graded { get; set; }
        public float ScoreMaximum { get; set; } = 100;
        public string? Tag { get; set; }
    }
}
