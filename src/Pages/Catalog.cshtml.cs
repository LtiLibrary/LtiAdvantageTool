using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using AdvantageTool.Utility;
using LtiAdvantage.DeepLinking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdvantageTool.Pages
{
    /// <summary>
    /// This is the catalog page for the Deep Linking workflow.
    /// </summary>
    public class CatalogModel : PageModel
    {
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
        public IActionResult OnPostAssignActivities()
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

            Debug.WriteLine(response.ToJsonString());

            return Redirect(LtiRequest.DeepLinkingSettings.DeepLinkReturnUrl);
        }

        private IList<Activity> GenerateActivities(int count)
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
