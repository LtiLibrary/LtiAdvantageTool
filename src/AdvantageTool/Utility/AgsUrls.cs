namespace AdvantageTool.Utility;

/// <summary>URL helpers for the Assignment and Grade Services.</summary>
public static class AgsUrls
{
    public static bool IsSameOrigin(string? url, string? other) =>
        Uri.TryCreate(url, UriKind.Absolute, out var a)
        && Uri.TryCreate(other, UriKind.Absolute, out var b)
        && a.GetLeftPart(UriPartial.Authority) == b.GetLeftPart(UriPartial.Authority);
}
