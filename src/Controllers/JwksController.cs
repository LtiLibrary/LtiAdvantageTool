using AdvantageTool.Data;
using AdvantageTool.Utility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Controllers
{
    /// <inheritdoc />
    /// <summary>
    /// Implements the OpenID Connect JWK Set Service.
    /// </summary>
    [Route(JwksUri)]
    [ApiController]
    public class JwksController : ControllerBase
    {
        public const string JwksUri = "oauth2/jwks";

        private readonly ApplicationDbContext _context;

        public JwksController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Return a JWK Set for all clients.
        /// </summary>
        /// <returns></returns>
        public IActionResult OnGet()
        {
            var jsonWebKeySet = new JsonWebKeySet();

            foreach (var client in _context.Clients)
            {
                var key = PemHelper.PublicKeyFromPemString(client.PublicKey);
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