using AdvantageTool.Data;
using LtiAdvantageLibrary.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Controllers
{
    [Route(".well-known/openid-configuration/jwks", Name = "jwks")]
    [ApiController]
    public class JwksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public JwksController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            var jsonWebKeySet = new JsonWebKeySet();

            foreach (var client in _context.Clients)
            {
                var key = RsaHelper.PublicKeyFromPemString(client.PublicKey);
                var parameters = key.Parameters;
                var jwk = new JsonWebKey
                {
                    Alg = SecurityAlgorithms.RsaSha256,
                    Kty = JsonWebAlgorithmsKeyTypes.RSA,
                    Use = JsonWebKeyUseNames.Sig,
                    Kid = client.KeyId,
                    E = Base64UrlEncoder.Encode(parameters.Exponent),
                    N = Base64UrlEncoder.Encode(parameters.Modulus)
                };
                jsonWebKeySet.Keys.Add(jwk);
            }
            return new JsonResult(jsonWebKeySet);
        }
    }
}