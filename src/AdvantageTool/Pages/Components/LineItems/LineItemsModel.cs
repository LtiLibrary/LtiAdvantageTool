using AdvantageTool.Services;
using LtiAdvantage.AssignmentGradeServices;
using LtiAdvantage.Lti;
using LtiAdvantage.NamesRoleProvisioningService;

namespace AdvantageTool.Pages.Components.LineItems;

public class LineItemsModel(Launch launch)
{
    public string LaunchId { get; } = launch.Id;
    public string? Status { get; set; }
    public LtiResourceLinkRequest? LtiRequest { get; set; }

    // Filters, read from the query string of the tool page.
    public string? ResourceLinkIdFilter { get; set; }
    public string? TagFilter { get; set; }
    public string? ResourceIdFilter { get; set; }
    public string? ResultsUserFilter { get; set; }
    public string? RoleFilter { get; set; }
    public string? RlidFilter { get; set; }
    public int? Limit { get; set; }

    public IList<MyLineItem> LineItems { get; set; } = [];
    public int LineItemPages { get; set; }
    public IList<Member> Members { get; set; } = [];
    public int MemberPages { get; set; }

    public string NameOf(string? userId) =>
        Members.FirstOrDefault(m => m.UserId == userId) is { } m ? $"{m.FamilyName}, {m.GivenName}" : userId ?? "";
}

public class MyLineItem
{
    public string Header { get; set; } = string.Empty;
    public LineItem AgsLineItem { get; set; } = default!;
    public ResultContainer? Results { get; set; }
}
