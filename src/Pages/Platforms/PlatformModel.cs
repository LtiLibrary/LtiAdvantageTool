using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using IdentityModel.Client;

namespace AdvantageTool.Pages.Platforms
{
    /// <summary>
    /// Platform configuration.
    /// </summary>
    public class PlatformModel
    {
        public PlatformModel() { }

        public PlatformModel(Platform platform)
        {
            Id = platform.Id;
            AccessTokenUrl = platform.AccessTokenUrl;
            Issuer = platform.Issuer;
            JwkSetUrl = platform.JwkSetUrl;
            Name = platform.Name;

            ClientId = platform.ClientId;
            PrivateKey = platform.PrivateKey;
        }

        public int Id { get; set; }

        #region Platform properties

        [LocalhostUrl]
        [Required]
        [Display(Name = "Access Token URL", Description = "The tool can request an access token using this endpoint (for example to use the Names and Role Service).")]
        public string AccessTokenUrl { get; set; }

        [Required]
        [Display(Name = "Issuer", Description = "This is the Issuer for all messages that originate from the Platform.")]
        public string Issuer { get; set; }

        [LocalhostUrl]
        [Required]
        [Display(Name = "JWK Set Url", Description = "The tool can retrieve the platform's public keys using this endpoint.")]
        public string JwkSetUrl { get; set; }

        [Required]
        [Display(Name = "Display Name")]
        public string Name { get; set; }

        #endregion

        #region Tool properties

        /// <summary>
        /// Tool's OpenID Client ID
        /// </summary>
        [Required]
        [Display(Name = "Client ID")]
        public string ClientId { get; set; }

        /// <summary>
        /// Tool's private key in PEM format
        /// </summary>
        [Required]
        [Display(Name = "Private Key", Description = "This is the private key the tool will use to sign client credentials.")]
        public string PrivateKey { get; set; }

        #endregion

        public async Task DiscoverEndpoints(IHttpClientFactory factory)
        {
            var httpClient = factory.CreateClient();
            var disco = await httpClient.GetDiscoveryDocumentAsync(Issuer);
            if (!disco.IsError)
            {
                AccessTokenUrl = disco.TokenEndpoint;
                JwkSetUrl = disco.JwksUri;
            }
            else if (AccessTokenUrl.IsPresent())
            {
                disco = await httpClient.GetDiscoveryDocumentAsync(AccessTokenUrl);
                if (!disco.IsError)
                {
                    AccessTokenUrl = disco.TokenEndpoint;
                    JwkSetUrl = disco.JwksUri;
                }
                else if (JwkSetUrl.IsPresent())
                {
                    disco = await httpClient.GetDiscoveryDocumentAsync(JwkSetUrl);
                    if (!disco.IsError)
                    {
                        AccessTokenUrl = disco.TokenEndpoint;
                        JwkSetUrl = disco.JwksUri;
                    }
                }
            }
        }

        public void UpdateEntity(Platform platform)
        {
            platform.AccessTokenUrl = AccessTokenUrl;
            platform.Name = Name;
            platform.Issuer = Issuer;
            platform.JwkSetUrl = JwkSetUrl;

            platform.ClientId = ClientId;
            platform.PrivateKey = PrivateKey;
        }
    }
}
