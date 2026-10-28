using System.Text.Json;
using AdvantageTool.Services;
using AdvantageTool.Utility;
using IdentityModel.Client;
using LtiAdvantage;
using LtiAdvantage.CourseGroupsService;
using LtiAdvantage.Lti;
using LtiAdvantage.NamesRoleProvisioningService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace AdvantageTool.Pages.Components.CourseGroups;

/// <summary>
/// Course Groups service: the group sets and groups of the launch's course, and who is in each
/// group. Shown only when the launch carries the groups service claim.
/// </summary>
public class CourseGroupsViewComponent(AccessTokenService tokens, IHttpClientFactory httpClientFactory) : ViewComponent
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IViewComponentResult> InvokeAsync(Launch launch)
    {
        var model = new CourseGroupsModel();
        var lti = new LtiResourceLinkRequest(launch.Payload);
        var service = lti.CourseGroupsService;
        if (service is null) return View(model);

        // Group membership comes from Names & Roles, so ask for both scopes.
        var token = await tokens.GetAccessTokenAsync(launch.PlatformId,
            $"{Constants.LtiScopes.Gs.ContextGroupReadonly} {Constants.LtiScopes.Nrps.MembershipReadonly}");
        if (token.IsError) { model.Status = token.Error; return View(model); }

        var http = httpClientFactory.CreateClient();
        http.SetBearerToken(token.AccessToken!);
        try
        {
            if (!string.IsNullOrEmpty(service.ContextGroupSetsUrl))
            {
                (model.Sets, _) = await Paging.GetAllPagesAsync(http, service.ContextGroupSetsUrl,
                    Constants.MediaTypes.ContextGroupSetContainer,
                    json => JsonSerializer.Deserialize<ContextGroupSetContainer>(json, JsonOptions)?.Sets ?? []);
            }
            (model.Groups, _) = await Paging.GetAllPagesAsync(http, service.ContextGroupsUrl,
                Constants.MediaTypes.ContextGroupContainer,
                json => JsonSerializer.Deserialize<ContextGroupContainer>(json, JsonOptions)?.Groups ?? []);

            // groups=true asks Names & Roles to add each member's group_enrollments; the platform
            // only does so when the token also holds the groups scope.
            if (lti.NamesRoleService?.ContextMembershipUrl is { Length: > 0 } membershipUrl)
            {
                (model.Members, _) = await Paging.GetAllPagesAsync(http,
                    QueryHelpers.AddQueryString(membershipUrl, "groups", "true"),
                    Constants.MediaTypes.MembershipContainer,
                    json => JsonSerializer.Deserialize<MembershipContainer>(json, JsonOptions)?.Members ?? []);
            }
        }
        catch (Exception e) { model.Status = e.Message; }

        return View(model);
    }
}

public class CourseGroupsModel
{
    public string? Status { get; set; }
    public List<GroupSet> Sets { get; set; } = [];
    public List<Group> Groups { get; set; } = [];
    public List<Member> Members { get; set; } = [];

    public IEnumerable<Group> GroupsIn(GroupSet set) => Groups.Where(g => g.SetIds?.Contains(set.Id) == true);
    public IEnumerable<Group> GroupsWithoutSet => Groups.Where(g => g.SetIds is null || g.SetIds.Length == 0 || !g.SetIds.Any(id => Sets.Any(s => s.Id == id)));

    public IEnumerable<Member> MembersOf(Group group) =>
        Members.Where(m => m.GroupEnrollments?.Any(e => e.GroupId == group.Id) == true)
            .OrderBy(m => m.FamilyName).ThenBy(m => m.GivenName);
}
