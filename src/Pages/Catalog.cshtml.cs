using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using LtiAdvantage.DeepLinking;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Pages
{
    /// <summary>
    /// This is the catalog page for the Deep Linking workflow.
    /// </summary>
    public class CatalogModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CatalogModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public IList<Activity> Activities { get; set; }

        [BindProperty]
        public string IdToken { get; set; }

        /// <summary>
        /// The error discovered while parsing the request.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Wrapper around the LTI request to make rendering the catalog easier.
        /// </summary>
        public LtiDeepLinkingRequest LtiRequest { get; set; }

        /// <summary>
        /// The parsed JWT to render on the page for debugging.
        /// </summary>
        public JwtSecurityToken Token { get; set; }

        /// <summary>
        /// Handle the LTI request to launch deep linking. The token was validated by the Tool page.
        /// </summary>
        /// <returns></returns>
        public IActionResult OnGet(string idToken)
        {
            if (string.IsNullOrEmpty(idToken))
            {
                Error = $"{nameof(idToken)} is missing or empty";
                return Page();
            }

            IdToken = idToken;

            var handler = new JwtSecurityTokenHandler();
            Token = handler.ReadJwtToken(IdToken);
            LtiRequest = new LtiDeepLinkingRequest(Token.Payload);

            // Fill the catalog with choices
            Activities = GenerateActivities(10);

            return Page();
        }

        /// <summary>
        /// Handle the assignments.
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> OnPostAssignActivities()
        {
            var handler = new JwtSecurityTokenHandler();
            Token = handler.ReadJwtToken(IdToken);
            LtiRequest = new LtiDeepLinkingRequest(Token.Payload);

            var response = new LtiDeepLinkingResponse
            {
                Data = LtiRequest.DeepLinkingSettings.Data,
                DeploymentId = LtiRequest.DeploymentId
            };
            var contentItems = new List<ContentItemType>();
            foreach (var activity in Activities)
            {
                if (activity.Selected)
                {
                    contentItems.Add(new LtiLinkItemType
                    {
                        Title = activity.Title,
                        Text = activity.Description,
                        Url = Url.Page("./Tool", null, null, Request.Scheme)
                    });
                }
            }

            response.ContentItems = contentItems.ToArray();

            response.AddClaim(new Claim(JwtRegisteredClaimNames.Iss, LtiRequest.Aud[0]));
            response.AddClaim(new Claim(JwtRegisteredClaimNames.Aud, LtiRequest.Iss));
            response.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, LtiRequest.Sub));
            response.AddClaim(new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(DateTime.UtcNow).ToString()));
            response.AddClaim(new Claim(JwtRegisteredClaimNames.Nbf, EpochTime.GetIntDate(DateTime.UtcNow.AddSeconds(-5)).ToString()));
            response.AddClaim(new Claim(JwtRegisteredClaimNames.Exp, EpochTime.GetIntDate(DateTime.UtcNow.AddMinutes(5)).ToString()));
            response.AddClaim(new Claim(JwtRegisteredClaimNames.Nonce, LtiAdvantage.Lti.LtiRequest.GenerateCryptographicNonce()));

            var platform = await _context.GetPlatformByIssuerAsync(LtiRequest.Iss);
            var credentials = PemHelper.SigningCredentialsFromPemString(platform.PrivateKey);
            var jwt = handler.WriteToken(new JwtSecurityToken(new JwtHeader(credentials), response));

            return Post("id_token", jwt, LtiRequest.DeepLinkingSettings.DeepLinkReturnUrl);
        }

        private static ContentResult Post(string name, string value, string url)
        {
            return new ContentResult
            {
                Content = "<html><head><title></title></head><body onload=\"document.contentitems.submit()\">"
                          + $"<form name=\"contentitems\" method=\"post\" action=\"{url}\">"
                          + $"<input type=\"hidden\" name=\"{name}\" value=\"{value}\" /></body></html>",
                ContentType = "text/html",
                StatusCode = StatusCodes.Status200OK
            };
        }

        private static IList<Activity> GenerateActivities(int count)
        {
            var activities = new List<Activity>();

            for (int index = 0; index < count; index++)
            {
                activities.Add(new Activity
                {
                    Id = index,
                    Title = $"Activity {index}",
                    Description = $"This is the description for Activity {index}."
                });
            }

            return activities;
        }

        public class Activity
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public bool Selected { get; set; }
        }
    }
}
