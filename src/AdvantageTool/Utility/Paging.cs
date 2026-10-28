using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace AdvantageTool.Utility;

/// <summary>
/// AGS and NRPS page large results with an RFC 8288 Link header: rel="next" points at the
/// next page. A tool must follow it to see every line item or member.
/// </summary>
public static partial class Paging
{
    /// <summary>Upper bound on pages fetched, so a misbehaving platform cannot loop us forever.</summary>
    private const int MaxPages = 20;

    public static async Task<(List<T> Items, int Pages)> GetAllPagesAsync<T>(
        HttpClient http, string url, string mediaType, Func<string, IEnumerable<T>> read)
    {
        var items = new List<T>();
        var pages = 0;
        string? next = url;
        while (next is not null && pages < MaxPages)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, next);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
            using var response = await http.SendAsync(request);
            await ServiceError.ThrowIfFailedAsync(response);
            items.AddRange(read(await response.Content.ReadAsStringAsync()));
            pages++;
            next = NextLink(response);
        }
        return (items, pages);
    }

    public static string? NextLink(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values)) return null;
        foreach (var match in values.SelectMany(v => LinkPattern().Matches(v)))
        {
            if (match.Groups["rel"].Value.Split(' ').Contains("next")) return match.Groups["url"].Value;
        }
        return null;
    }

    [GeneratedRegex("<(?<url>[^>]+)>[^,]*?rel=\"?(?<rel>[^\";,]+)\"?")]
    private static partial Regex LinkPattern();
}
