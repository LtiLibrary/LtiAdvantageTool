namespace AdvantageTool.Utility;

/// <summary>URL helpers for the Assignment and Grade Services.</summary>
public static class AgsUrls
{
    /// <summary>AGS 2.0 §3.4: the results endpoint is the line item URL with /results appended to the path (query kept).</summary>
    public static string Results(string lineItemUrl) => Append(lineItemUrl, "results");

    public static bool IsSameOrigin(string? url, string? other) =>
        Uri.TryCreate(url, UriKind.Absolute, out var a)
        && Uri.TryCreate(other, UriKind.Absolute, out var b)
        && a.GetLeftPart(UriPartial.Authority) == b.GetLeftPart(UriPartial.Authority);

    private static string Append(string url, string segment)
    {
        var uri = new Uri(url);
        return $"{uri.GetLeftPart(UriPartial.Path).TrimEnd('/')}/{segment}{uri.Query}";
    }
}
