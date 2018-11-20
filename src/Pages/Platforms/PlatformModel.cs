using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using IdentityModel.Client;
using Microsoft.AspNetCore.Identity;

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
            ClientId = platform.ClientId;
            ClientPrivateKey = platform.ClientPrivateKey;
            ClientPublicKey = platform.ClientPublicKey;
            Issuer = platform.Issuer;
            JsonWebKeySetUrl = platform.JsonWebKeySetUrl;
            Name = platform.Name;
        }

        public int Id { get; set; }

        [LocalhostUrl]
        [Required]
        [Display(Name = "Access Token URL", Description = "If the Issuer supports Open ID Connect Discovery, then you can enter the Issuer URL and the token URL will be discovered.")]
        public string AccessTokenUrl { get; set; }

        [Required]
        [Display(Name = "Client ID")]
        public string ClientId { get; set; }

        [Required]
        [Display(Name = "Private Key", Description = "This is the private key the tool will use to sign client credentials.")]
        public string ClientPrivateKey { get; set; }

        [Display(Name = "Public Key", Description = "This is the public key the platform should use to validate client credentials.")]
        public string ClientPublicKey { get; set; }

        [Required]
        [Display(Name = "Issuer", Description = "This is the Issuer for all messages that originate from the Platform.")]
        public string Issuer { get; set; }

        [LocalhostUrl]
        [Required]
        [Display(Name = "JSON Web Key Set URL", Description = "If the Issuer supports Open ID Connect Discovery, then you can enter the Issuer URL and the JWKS URL will be discovered.")]
        public string JsonWebKeySetUrl { get; set; }

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
                JsonWebKeySetUrl = disco.JwksUri;
            }
            else if (AccessTokenUrl.IsPresent())
            {
                disco = await httpClient.GetDiscoveryDocumentAsync(AccessTokenUrl);
                if (!disco.IsError)
                {
                    AccessTokenUrl = disco.TokenEndpoint;
                    JsonWebKeySetUrl = disco.JwksUri;
                }
                else if (JsonWebKeySetUrl.IsPresent())
                {
                    disco = await httpClient.GetDiscoveryDocumentAsync(JsonWebKeySetUrl);
                    if (!disco.IsError)
                    {
                        AccessTokenUrl = disco.TokenEndpoint;
                        JsonWebKeySetUrl = disco.JwksUri;
                    }
                }
            }
        }

        public void FillEntity(Platform platform)
        {
            platform.AccessTokenUrl = AccessTokenUrl;
            platform.ClientId = ClientId;
            platform.ClientPrivateKey = ClientPrivateKey.IsPresent()
                ? ClientPrivateKey.Replace("\r\n\r\n", "\r\n")
                : null;
            platform.ClientPublicKey = ClientPublicKey.IsPresent()
                ? ClientPublicKey.Replace("\r\n\r\n", "\r\n")
                : null;
            platform.Name = Name;
            platform.Issuer = Issuer;
            platform.JsonWebKeySetUrl = JsonWebKeySetUrl;
        }
    }
}
