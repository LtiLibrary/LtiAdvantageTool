using System.Net.Http.Headers;
using LtiAdvantage.IdentityModel.Client;

namespace AdvantageTool.Utility;

/// <summary>
/// AGS and NRPS page large results with an RFC 8288 Link header: rel="next" points at the
/// next page. A tool must follow it to see every line item or member.
/// </summary>
/// <remarks>
/// The library's <c>HttpClientLtiServiceExtensions</c> (GetLineItemsAsync, GetMembershipAsync, …)
/// do all of this in one call. The sample keeps its own loop only to show how many pages it read;
/// it uses the library's <see cref="HttpResponseMessageLtiExtensions.GetNextPageUrl(HttpResponseMessage, Uri)"/>
/// to read the header and <see cref="HttpResponseMessageLtiExtensions.EnsureLtiSuccessAsync"/> for errors.
/// </remarks>
public static class Paging
{
    /// <summary>Upper bound on pages fetched, so a misbehaving platform cannot loop us forever.</summary>
    private const int MaxPages = 20;

    public static async Task<(List<T> Items, int Pages)> GetAllPagesAsync<T>(
        HttpClient http, string url, string mediaType, Func<string, IEnumerable<T>> read)
    {
        var first = new Uri(url);
        var items = new List<T>();
        var pages = 0;
        Uri? next = first;
        while (next is not null && pages < MaxPages)
        {
            // The access token goes with every page: never follow a link to another host.
            if (next.GetLeftPart(UriPartial.Authority) != first.GetLeftPart(UriPartial.Authority))
                throw new InvalidOperationException($"The next page link {next} is on a different origin than {first}.");

            using var request = new HttpRequestMessage(HttpMethod.Get, next);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
            using var response = await http.SendAsync(request);
            await response.EnsureLtiSuccessAsync();
            items.AddRange(read(await response.Content.ReadAsStringAsync()));
            pages++;
            next = response.GetNextPageUrl(next);
        }
        return (items, pages);
    }
}
