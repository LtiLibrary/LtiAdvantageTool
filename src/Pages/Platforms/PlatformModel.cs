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
        }

        public int Id { get; set; }

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
        }
    }
}
