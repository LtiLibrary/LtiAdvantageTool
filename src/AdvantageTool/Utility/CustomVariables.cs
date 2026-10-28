namespace AdvantageTool.Utility;

/// <summary>
/// LTI custom variable substitution (LTI 1.3 §5.4.1, LTI Advantage appendix): the tool asks for a
/// value by sending a custom parameter whose value is a $Variable name; the platform replaces it
/// at launch. A platform that does not support a variable must leave it as is.
/// </summary>
public static class CustomVariables
{
    /// <summary>Sent with Dynamic Registration, so the platform adds them to every launch.</summary>
    public static Dictionary<string, string> ForRegistration() => new()
    {
        ["user_id"] = "$User.id",
        ["user_full_name"] = "$Person.name.full",
        ["context_history"] = "$Context.id.history",
        ["platform_product"] = "$ToolPlatform.productFamilyCode",
    };

    /// <summary>Sent on each deep-linked LTI link, so they come back on launches of that link.</summary>
    public static Dictionary<string, string> ForLink() => new()
    {
        ["resource_link_title"] = "$ResourceLink.title",
        ["resource_link_history"] = "$ResourceLink.id.history",
        ["context_history"] = "$Context.id.history",
        ["groups"] = "$Membership.course.groupIds",
    };

    /// <summary>True when the platform left the value as the literal variable name (not substituted).</summary>
    public static bool IsUnresolved(string? value) =>
        value is { Length: > 1 } && value[0] == '$' && char.IsLetter(value[1]) && !value.Contains(' ');
}
