using LtiAdvantage.IdentityModel.Client;

namespace AdvantageTool.Utility;

/// <summary>
/// Reads what a platform said when an AGS, NRPS, Course Groups or Assessment Control call failed.
/// The library's <see cref="HttpResponseMessageLtiExtensions.EnsureLtiSuccessAsync"/> does the work:
/// it reads an RFC 9457 ProblemDetails body (title, detail), which LtiAdvantage platforms send,
/// falls back to the WWW-Authenticate challenge when there is no body (a token without the
/// needed scope is refused before any controller runs), and otherwise shows the start of the body.
/// </summary>
public static class ServiceError
{
    public static async Task<string> DescribeAsync(HttpResponseMessage response)
    {
        try
        {
            await response.EnsureLtiSuccessAsync();
            return $"{(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (LtiServiceException e)
        {
            return e.Message;
        }
    }

    /// <summary>Throws an <see cref="LtiServiceException"/> describing the platform's answer when the call failed.</summary>
    public static Task ThrowIfFailedAsync(HttpResponseMessage response) => response.EnsureLtiSuccessAsync();
}
