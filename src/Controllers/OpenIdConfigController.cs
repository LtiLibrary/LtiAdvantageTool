using System;
using AdvantageTool.Utility;
using Microsoft.AspNetCore.Mvc;

namespace AdvantageTool.Controllers
{
    /// <summary>
    /// Implements the OpenID Connect Discovery Service.
    /// </summary>
    [Route(OidcWellknownConfigUri)]
    [ApiController]
    public class OpenIdConfigController : ControllerBase
    {
        public const string OidcWellknownConfigUri = ".well-known/openid-configuration";

        /// <summary>
        /// Return the OpenID Connect configuration.
        /// </summary>
        /// <returns></returns>
        public IActionResult OnGet()
        {
            var issuer = new Uri($"{Request.Scheme}://{Request.Host}");
            return new JsonResult(new OpenIdConfig
            {
                Issuer = issuer.AbsoluteUri,
                JwksUri = issuer.AbsoluteUri.EnsureTrailingSlash() + JwksController.JwksUri
            });
        }
    }
}