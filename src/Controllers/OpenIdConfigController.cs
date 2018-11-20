using System;
using AdvantageTool.Utility;
using Microsoft.AspNetCore.Mvc;

namespace AdvantageTool.Controllers
{
    [Route(".well-known/openid-configuration")]
    [ApiController]
    public class OpenIdConfigController : ControllerBase
    {
        public IActionResult OnGet()
        {
            var issuer = new Uri($"{Request.Scheme}://{Request.Host}");
            return new JsonResult(new OpenIdConfig
            {
                Issuer = issuer.AbsoluteUri,
                JwksUri = issuer.AbsoluteUri.EnsureTrailingSlash() + ".well-known/openid-configuration/jwks"
            });
        }
    }
}