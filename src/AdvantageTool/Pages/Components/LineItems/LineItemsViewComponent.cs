using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text.Json;
using AdvantageTool.Services;
using IdentityModel.Client;
using LtiAdvantage;
using LtiAdvantage.AssignmentGradeServices;
using LtiAdvantage.Lti;
using LtiAdvantage.NamesRoleProvisioningService;
using LtiAdvantage.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace AdvantageTool.Pages.Components.LineItems;

public class LineItemsViewComponent(AccessTokenService tokens, IHttpClientFactory httpClientFactory) : ViewComponent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new IsoDateTimeConverter() },
    };

    public async Task<IViewComponentResult> InvokeAsync(string? idToken)
    {
        var model = new LineItemsModel(idToken);
        if (string.IsNullOrEmpty(idToken)) { model.Status = "id_token missing"; return View(model); }

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(idToken);
        model.LtiRequest = new LtiResourceLinkRequest(jwt.Payload);
        var lti = model.LtiRequest;

        if (lti.AssignmentGradeServices is null) { model.Status = "Assignment & Grade Services not present in launch."; return View(model); }
        model.LineItemUrl = lti.AssignmentGradeServices.LineItemUrl;

        var scopes = string.Join(" ",
            Constants.LtiScopes.Ags.LineItem,
            Constants.LtiScopes.Ags.ResultReadonly,
            Constants.LtiScopes.Nrps.MembershipReadonly);
        var token = await tokens.GetAccessTokenAsync(lti.Iss!, scopes);
        if (token.IsError) { model.Status = token.Error; return View(model); }

        var http = httpClientFactory.CreateClient();
        http.SetBearerToken(token.AccessToken!);

        try
        {
            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(Constants.MediaTypes.LineItemContainer));
            var liJson = await http.GetStringAsync(lti.AssignmentGradeServices.LineItemsUrl);
            var lineItems = JsonSerializer.Deserialize<LineItem[]>(liJson, JsonOptions) ?? [];
            model.LineItems = lineItems.Select(i => new MyLineItem
            {
                AgsLineItem = i,
                Header = i.Label ?? $"Tag: {i.Tag}",
            }).ToList();
        }
        catch (Exception e) { model.Status = e.Message; return View(model); }

        if (lti.NamesRoleService is not null)
        {
            try
            {
                http.DefaultRequestHeaders.Accept.Clear();
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(Constants.MediaTypes.MembershipContainer));
                var memJson = await http.GetStringAsync(lti.NamesRoleService.ContextMembershipUrl);
                var container = JsonSerializer.Deserialize<MembershipContainer>(memJson, JsonOptions);
                if (container?.Members is not null)
                {
                    foreach (var m in container.Members.OrderBy(m => m.FamilyName).ThenBy(m => m.GivenName))
                    {
                        if (!string.IsNullOrEmpty(m.UserId))
                            model.Members.TryAdd(m.UserId, $"{m.FamilyName}, {m.GivenName}");
                    }
                }
            }
            catch (Exception e) { model.Status = e.Message; }
        }

        try
        {
            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(Constants.MediaTypes.ResultContainer));
            foreach (var li in model.LineItems)
            {
                var lineItemUri = new Uri(li.AgsLineItem.Id!);
                var url = $"{lineItemUri.GetLeftPart(UriPartial.Path).TrimEnd('/')}/results{lineItemUri.Query}";
                var json = await http.GetStringAsync(url);
                li.Results = JsonSerializer.Deserialize<ResultContainer>(json, JsonOptions);
            }
        }
        catch (Exception e) { model.Status = e.Message; }

        return View(model);
    }
}
