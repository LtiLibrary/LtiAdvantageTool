using LtiAdvantage.AssignmentGradeServices;
using LtiAdvantage.Lti;

namespace AdvantageTool.Pages.Components.LineItems;

public class LineItemsModel(string? idToken)
{
    public string IdToken { get; } = idToken ?? string.Empty;
    public string? Status { get; set; }
    public string? LineItemUrl { get; set; }
    public LtiResourceLinkRequest? LtiRequest { get; set; }
    public IList<MyLineItem> LineItems { get; set; } = [];
    public IDictionary<string, string> Members { get; set; } = new Dictionary<string, string>();
}

public class MyLineItem
{
    public string Header { get; set; } = string.Empty;
    public LineItem AgsLineItem { get; set; } = default!;
    public ResultContainer? Results { get; set; }
}
