using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace AdvantageTool.Utility;

/// <summary>
/// LTI messages from the tool to the platform (deep linking response, start assessment) are
/// sent as a form post from the browser: this page posts itself to the platform on load.
/// </summary>
public static class AutoPost
{
    public static ContentResult Form(string url, IDictionary<string, string> fields)
    {
        var html = new StringBuilder();
        html.Append("<html><body onload=\"document.forms[0].submit()\"><form method=\"post\" action=\"")
            .Append(WebUtility.HtmlEncode(url)).Append("\">");
        foreach (var (name, value) in fields)
        {
            html.Append("<input type=\"hidden\" name=\"").Append(WebUtility.HtmlEncode(name))
                .Append("\" value=\"").Append(WebUtility.HtmlEncode(value)).Append("\"/>");
        }
        html.Append("<noscript><button type=\"submit\">Continue</button></noscript></form></body></html>");
        return new ContentResult { Content = html.ToString(), ContentType = "text/html", StatusCode = StatusCodes.Status200OK };
    }
}
