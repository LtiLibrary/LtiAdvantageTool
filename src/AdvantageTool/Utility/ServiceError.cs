using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace AdvantageTool.Utility;

/// <summary>
/// Reads what a platform said when an AGS, NRPS, Course Groups or Assessment Control call failed.
/// LtiAdvantage platforms answer with an RFC 9457 ProblemDetails body (title, status, detail);
/// other platforms may send nothing, plain text or their own JSON.
/// </summary>
public static class ServiceError
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<string> DescribeAsync(HttpResponseMessage response)
    {
        var status = $"{(int)response.StatusCode} {response.ReasonPhrase}";
        var body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body)) return status;

        try
        {
            var problem = JsonSerializer.Deserialize<ProblemDetails>(body, JsonOptions);
            if (problem is not null && (problem.Title is not null || problem.Detail is not null))
                return string.Join(" · ", new[] { status, problem.Title, problem.Detail }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        catch (JsonException) { }

        // Not ProblemDetails: show the start of whatever came back.
        return $"{status} · {(body.Length > 300 ? body[..300] + "…" : body)}";
    }

    /// <summary>Throws a <see cref="ServiceException"/> describing the platform's answer when the call failed.</summary>
    public static async Task ThrowIfFailedAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) throw new ServiceException(await DescribeAsync(response));
    }
}

public class ServiceException(string message) : Exception(message);
