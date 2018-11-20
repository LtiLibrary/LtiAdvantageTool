using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace AdvantageTool.Controllers
{
    [Route(".well-known/openid-configuration/jwks", Name = "jwks")]
    [ApiController]
    public class JwksController : ControllerBase
    {
        private readonly SigningCredentials _signingCredentials;

        public JwksController(SigningCredentials signingCredentials)
        {
            _signingCredentials = signingCredentials;
        }

        public IActionResult OnGet()
        {
            var keyset = new JsonWebKeySet();
            var key = (RsaSecurityKey) _signingCredentials.Key;
            var parameters = key.Parameters;
            var jwk = new JsonWebKey
            {
                Alg = SecurityAlgorithms.RsaSha256,
                Kty = JsonWebAlgorithmsKeyTypes.RSA,
                Use = JsonWebKeyUseNames.Sig,
                Kid = key.KeyId,
                E = Base64UrlEncoder.Encode(parameters.Exponent),
                N = Base64UrlEncoder.Encode(parameters.Modulus)
            };
            keyset.Keys.Add(jwk);

            return new JsonResult(keyset);
        }
    }
}