using System.Text.Json;
using AdvantageTool.Services;
using AdvantageTool.Utility;
using IdentityModel.Client;
using LtiAdvantage;
using LtiAdvantage.AssignmentGradeServices;
using LtiAdvantage.IdentityModel.Client;
using LtiAdvantage.Lti;
using LtiAdvantage.NamesRoleProvisioningService;
using LtiAdvantage.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace AdvantageTool.Pages.Components.LineItems;

public class LineItemsViewComponent(AccessTokenService tokens, IHttpClientFactory httpClientFactory) : ViewComponent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new IsoDateTimeConverter() },
    };

    public async Task<IViewComponentResult> InvokeAsync(Launch launch)
    {
        var model = new LineItemsModel(launch) { LtiRequest = new LtiResourceLinkRequest(launch.Payload) };
        var lti = model.LtiRequest;
        ReadFilters(model);

        var scopes = string.Join(" ",
            Constants.LtiScopes.Ags.LineItem,
            Constants.LtiScopes.Ags.ResultReadonly,
            Constants.LtiScopes.Nrps.MembershipReadonly);
        var token = await tokens.GetAccessTokenAsync(launch.PlatformId, scopes);
        if (token.IsError) { model.Status = token.Error; return View(model); }

        var http = httpClientFactory.CreateClient();
        http.SetBearerToken(token.AccessToken!);

        // Names & Roles first, so line item results can show names.
        if (lti.NamesRoleService?.ContextMembershipUrl is { Length: > 0 } membershipUrl)
        {
            try
            {
                // NRPS 2.0 §2.3: role and rlid filter the members; limit sets the page size, and the
                // platform's Link header (rel="next") leads to the next page.
                var url = QueryHelpers.AddQueryString(membershipUrl, Query(
                    ("role", model.RoleFilter), ("rlid", model.RlidFilter), ("limit", model.Limit?.ToString())));
                (model.Members, model.MemberPages) = await Paging.GetAllPagesAsync(http, url,
                    Constants.MediaTypes.MembershipContainer,
                    json => JsonSerializer.Deserialize<MembershipContainer>(json, JsonOptions)?.Members ?? []);
                model.Members = model.Members.OrderBy(m => m.FamilyName).ThenBy(m => m.GivenName).ToList();
            }
            catch (Exception e) { model.Status = $"Names & Roles: {e.Message}"; }
        }

        if (lti.AssignmentGradeServices?.LineItemsUrl is not { Length: > 0 } lineItemsUrl) return View(model);

        try
        {
            // AGS 2.0 §3.2.5: the line item container can be filtered by resource_link_id,
            // resource_id and tag, and is paged with limit + Link headers like NRPS.
            var url = QueryHelpers.AddQueryString(lineItemsUrl, Query(
                ("resource_link_id", model.ResourceLinkIdFilter), ("resource_id", model.ResourceIdFilter),
                ("tag", model.TagFilter), ("limit", model.Limit?.ToString())));
            var (lineItems, pages) = await Paging.GetAllPagesAsync(http, url,
                Constants.MediaTypes.LineItemContainer,
                json => JsonSerializer.Deserialize<LineItem[]>(json, JsonOptions) ?? []);
            model.LineItemPages = pages;
            model.LineItems = lineItems.Select(i => new MyLineItem
            {
                AgsLineItem = i,
                Header = i.Label ?? $"Tag: {i.Tag}",
            }).ToList();
        }
        catch (Exception e) { model.Status = $"Line items: {e.Message}"; return View(model); }

        try
        {
            foreach (var li in model.LineItems.Where(li => !string.IsNullOrEmpty(li.AgsLineItem.Id)))
            {
                // AGS 2.0 §3.4: user_id narrows the results to one user.
                // The library's GetResultsAsync adds /results and the filter, and follows paging.
                var results = await http.GetResultsAsync(token.AccessToken!, li.AgsLineItem.Id!, model.ResultsUserFilter);
                li.Results = new ResultContainer(results);
            }
        }
        catch (Exception e) { model.Status = $"Results: {e.Message}"; }

        return View(model);
    }

    private void ReadFilters(LineItemsModel model)
    {
        string? Get(string name) => Request.Query[name] is { Count: > 0 } v && !string.IsNullOrWhiteSpace(v[0]) ? v[0] : null;
        model.ResourceLinkIdFilter = Get("resource_link_id");
        model.TagFilter = Get("tag");
        model.ResourceIdFilter = Get("resource_id");
        model.ResultsUserFilter = Get("user_id");
        model.RoleFilter = Get("role");
        model.RlidFilter = Get("rlid");
        model.Limit = int.TryParse(Get("limit"), out var limit) && limit > 0 ? limit : null;
    }

    private static Dictionary<string, string?> Query(params (string Name, string? Value)[] parameters) =>
        parameters.Where(p => !string.IsNullOrEmpty(p.Value)).ToDictionary(p => p.Name, p => p.Value);
}
