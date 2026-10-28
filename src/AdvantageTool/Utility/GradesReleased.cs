namespace AdvantageTool.Utility;

/// <summary>
/// The AGS 2.0 gradesReleased flag on the tool's forms: "true", "false", or empty for "let the
/// platform decide" (the property is left out).
/// </summary>
public static class GradesReleased
{
    public static bool? Parse(string? value) => value switch
    {
        "true" => true,
        "false" => false,
        _ => null,
    };

    public static string Describe(bool? value) => value switch
    {
        true => "grades released",
        false => "grades hidden from learners",
        null => "grade release: platform default",
    };
}
