using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using AdvantageTool.Utility;
using LtiAdvantage;
using LtiAdvantage.AssignmentGradeServices;
using LtiAdvantage.Lti;
using LtiAdvantage.NamesRoleProvisioningService;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace AdvantageTool.Pages.Components.LineItems
{
    public class LineItemsViewComponent : ViewComponent
    {
        private readonly AccessTokenService _accessTokenService;
        private readonly IHttpClientFactory _httpClientFactory;

        public LineItemsViewComponent(
            AccessTokenService accessTokenService,
            IHttpClientFactory httpClientFactory)
        {
            _accessTokenService = accessTokenService;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IViewComponentResult> InvokeAsync(string idToken)
        {
            var model = new LineItemsModel(idToken);
            if (idToken.IsMissing())
            {
                model.Status = $"{nameof(idToken)} is missing.";
                return View(model);
            }

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(idToken);
            model.LtiRequest = new LtiResourceLinkRequest(token.Payload);

            if (model.LtiRequest.AssignmentGradeServices == null)
            {
                model.Status = "Assignment and Grade Services not defined.";
                return View(model);
            }
            model.LineItemUrl = model.LtiRequest.AssignmentGradeServices.LineItemUrl;

            var tokenResponse = await _accessTokenService.GetAccessTokenAsync(
                model.LtiRequest.Iss, 
                string.Join(" ", 
                    Constants.LtiScopes.Ags.LineItem, 
                    Constants.LtiScopes.Ags.ResultReadonly,
                    Constants.LtiScopes.Nrps.MembershipReadonly));

            // The IMS reference implementation returns "Created" with success. 
            if (tokenResponse.IsError && tokenResponse.Error != "Created")
            {
                model.Status = tokenResponse.Error;
                return View(model);
            }

            // Get all the line items
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.SetBearerToken(tokenResponse.AccessToken);

                httpClient.DefaultRequestHeaders.Accept
                    .Add(new MediaTypeWithQualityHeaderValue(Constants.MediaTypes.LineItemContainer));

                using (var response = await httpClient.GetAsync(model.LtiRequest.AssignmentGradeServices?.LineItemsUrl))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        model.Status = response.ReasonPhrase;
                        return View(model);
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    model.LineItems = JsonConvert.DeserializeObject<List<LineItem>>(content)
                        .Select(i => new MyLineItem
                        {
                            AgsLineItem = i,
                            Header = i.Label ?? $"Tag: {i.Tag}"
                        })
                        .ToList();
                }
            }
            catch (Exception e)
            {
                model.Status = e.Message;
                return View();
            }

            // Get all the members of the course
            model.Members = new Dictionary<string, string>();

            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.SetBearerToken(tokenResponse.AccessToken);

                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept
                    .Add(new MediaTypeWithQualityHeaderValue(Constants.MediaTypes.MembershipContainer));

                using (var response = await httpClient.GetAsync(model.LtiRequest.NamesRoleService.ContextMembershipUrl))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        model.Status = response.ReasonPhrase;
                        return View(model);
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    var membership = JsonConvert.DeserializeObject<MembershipContainer>(content);
                    foreach (var member in membership.Members.OrderBy(m => m.FamilyName).ThenBy(m => m.GivenName))
                    {
                        if (!model.Members.ContainsKey(member.UserId))
                        {
                            model.Members.Add(member.UserId, $"{member.FamilyName}, {member.GivenName}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                model.Status = e.Message;
                return View(model);
            }

            // Get all the results
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.SetBearerToken(tokenResponse.AccessToken);

                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept
                    .Add(new MediaTypeWithQualityHeaderValue(Constants.MediaTypes.ResultContainer));

                foreach (var lineItem in model.LineItems)
                {
                    using (var response = await httpClient.GetAsync(lineItem.AgsLineItem.Id.EnsureTrailingSlash() + "results"))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            model.Status = response.ReasonPhrase;
                            return View(model);
                        }

                        var content = await response.Content.ReadAsStringAsync();
                        lineItem.Results = JsonConvert.DeserializeObject<ResultContainer>(content);
                    }
                }
            }
            catch (Exception e)
            {
                model.Status = e.Message;
            }

            return View(model);
        }
    }
}