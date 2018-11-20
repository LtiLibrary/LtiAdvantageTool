using Newtonsoft.Json;

namespace AdvantageTool.Controllers
{
    public class OpenIdConfig
    {
        [JsonProperty("issuer")]
        public string Issuer { get; set; }

        [JsonProperty("jwks_uri")]
        public string JwksUri { get; set; }
    }
}
