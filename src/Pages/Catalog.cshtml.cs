using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using LtiAdvantage.DeepLinking;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;
using RandomNameGeneratorLibrary;

namespace AdvantageTool.Pages
{
    /// <summary>
    /// This is the catalog page for the Deep Linking workflow.
    /// </summary>
    [IgnoreAntiforgeryToken]
    public class CatalogModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CatalogModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // The list of activities the user can choose from.
        [BindProperty]
        public IList<Activity> Activities { get; set; }

        // The signed JWT.
        [BindProperty]
        public string IdToken { get; set; }

        /// <summary>
        /// The error discovered while parsing the request.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Wrapper around the deep linking request to make rendering the page easier.
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
        public IActionResult OnPost()
        {
            if (string.IsNullOrEmpty(IdToken))
            {
                Error = $"{nameof(IdToken)} is missing or empty";
                return Page();
            }

            var handler = new JwtSecurityTokenHandler();
            Token = handler.ReadJwtToken(IdToken);
            LtiRequest = new LtiDeepLinkingRequest(Token.Payload);

            // Fill the catalog with choices
            if (LtiRequest.Context == null)
            {
                Activities = new List<Activity>
                {
                    new Activity
                    {
                        Id = 1,
                        Title = "Reports",
                        Description = "Reporting tool for admins.",
                        Selected = false
                    }
                };
            }
            else
            {
                Activities = GenerateActivities(12);
            }

            return Page();
        }

        /// <summary>
        /// Build and send the deep linking response.
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

            var contentItems = new List<ContentItem>();
            var customParameters = LtiRequest.Custom;
            foreach (var activity in Activities)
            {
                if (activity.Selected)
                {
                    var contentItem = new LtiLinkItem
                    {
                        Title = activity.Title,
                        Text = activity.Description,
                        Url = Url.Page("./Tool", null, null, Request.Scheme),
                        Custom = new Dictionary<string, string>
                        {
                            { "activity_id", activity.Id.ToString() }
                        }
                    };

                    if (customParameters != null)
                    {
                        foreach (var keyValue in LtiRequest.Custom)
                        {
                            contentItem.Custom.TryAdd(keyValue.Key, keyValue.Value);
                        }
                    }

                    contentItems.Add(contentItem);
                }
            }

            response.ContentItems = contentItems.ToArray();
            response.AddClaim(new Claim(JwtRegisteredClaimNames.Iss, LtiRequest.Aud[0]));
            response.AddClaim(new Claim(JwtRegisteredClaimNames.Aud, LtiRequest.Iss));
            response.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, LtiRequest.Sub));
            response.AddClaim(new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(DateTime.UtcNow).ToString()));
            response.AddClaim(new Claim(JwtRegisteredClaimNames.Nbf, EpochTime.GetIntDate(DateTime.UtcNow.AddSeconds(-5)).ToString()));
            response.AddClaim(new Claim(JwtRegisteredClaimNames.Exp, EpochTime.GetIntDate(DateTime.UtcNow.AddMinutes(5)).ToString()));
            response.AddClaim(new Claim(JwtRegisteredClaimNames.Nonce, IdentityModel.CryptoRandom.CreateRandomKeyString(8)));

            var platform = await _context.GetPlatformByIssuerAsync(LtiRequest.Iss);
            var credentials = PemHelper.SigningCredentialsFromPemString(platform.PrivateKey);
            var jwt = handler.WriteToken(new JwtSecurityToken(new JwtHeader(credentials), response));

            return Post("id_token", jwt, LtiRequest.DeepLinkingSettings.DeepLinkReturnUrl);
        }

        /// <summary>
        /// Returns a <seealso cref="ContentResult"/> that automatically posts the
        /// name/value to a URL.
        /// </summary>
        /// <param name="name">The name of the data.</param>
        /// <param name="value">The value of the data.</param>
        /// <param name="url">The URL to post to.</param>
        /// <returns>The content result.</returns>
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

        /// <summary>
        /// Returns a random list of activities to display in the catalog.
        /// </summary>
        /// <param name="count">The number of activities to create.</param>
        /// <returns>The activities.</returns>
        private static IList<Activity> GenerateActivities(int count)
        {
            var placeNameGenerator = new PlaceNameGenerator();
            var placeNames = placeNameGenerator.GenerateMultiplePlaceNames(count).ToArray();
            var numberGenerator = new Random();

            var activities = new List<Activity>();

            for (var index = 0; index < count; index++)
            {
                var year = 1600 + numberGenerator.Next(0, 200);

                activities.Add(new Activity
                {
                    Id = index,
                    Title = $"The history of {placeNames[index]}",
                    Description = $"This activity traces the history of {placeNames[index]} from its founding in {year}."
                });
            }

            return activities;
        }

        /// <summary>
        /// An activity.
        /// </summary>
        public class Activity
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public bool Selected { get; set; }
        }
    }
}
