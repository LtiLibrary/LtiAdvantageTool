using Newtonsoft.Json;

namespace AdvantageTool.Controllers
{
    /// <summary>
    /// OpenID Connect configuration. Only supports the OpenID Connect JWKS Service.
    /// </summary>
    public class OpenIdConfig
    {
        [JsonProperty("issuer")]
        public string Issuer { get; set; }

        [JsonProperty("jwks_uri")]
        public string JwksUri { get; set; }
    }
}
