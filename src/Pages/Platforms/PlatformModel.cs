using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Threading.Tasks;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using IdentityModel.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AdvantageTool.Pages.Platforms
{
    /// <summary>
    /// Platform configuration.
    /// </summary>
    public class PlatformModel
    {
        public PlatformModel() { }

        public PlatformModel(HttpRequest request, IUrlHelper url)
        {
            DeepLinkingLaunchUrl = url.Page("/Tool", null, null, request.Scheme);
            LaunchUrl = url.Page("/Tool", null, null, request.Scheme);
            LoginUrl = url.Page("/OidcLogin", null, null, request.Scheme);
        }

        public PlatformModel(HttpRequest request, IUrlHelper url, Platform platform) :this(request, url)
        {
            Id = platform.Id;
            AccessTokenUrl = platform.AccessTokenUrl;
            AuthorizeUrl = platform.AuthorizeUrl;
            Issuer = platform.Issuer;
            JwkSetUrl = platform.JwkSetUrl;
            Name = platform.Name;

            ClientId = platform.ClientId;
            PrivateKey = platform.PrivateKey;
        }

        /// <summary>
        /// Primary key.
        /// </summary>
        public int Id { get; set; }

        #region Platform properties

        [LocalhostUrl]
        [Required]
        [Display(Name = "Access Token URL", Description = "The tool can request an access token using this endpoint (for example to use the Names and Role Service).")]
        public string AccessTokenUrl { get; set; }

        [LocalhostUrl]
        [Required]
        [Display(Name = "Authorization URL", Description = "The tool requests the identity token from this endpoint.")]
        public string AuthorizeUrl { get; set; }

        [Required]
        [Display(Name = "Issuer", Description = "This is the Issuer for all messages that originate from the Platform.")]
        public string Issuer { get; set; }

        [LocalhostUrl]
        [Required]
        [Display(Name = "JWK Set URL", Description = "The tool can retrieve the platform's public keys using this endpoint.")]
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
        /// Deep linking launch url.
        /// </summary>
        [LocalhostUrl]
        [Display(Name = "Deep Linking Launch URL", Description = "The URL to launch the tool's deep linking experience.")]
        public string DeepLinkingLaunchUrl { get; set; }

        /// <summary>
        /// Tool launch url.
        /// </summary>
        [Display(Name = "Launch URL", Description = "The URL to launch the tool.")]
        public string LaunchUrl { get; set; }

        /// <summary>
        /// OIDC login initiation url.
        /// </summary>
        [Display(Name = "Login URL", Description = "The URL to initiate OpenID Connect authorization.")]
        public string LoginUrl { get; set; }

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
                AuthorizeUrl = disco.AuthorizeEndpoint;
                JwkSetUrl = disco.JwksUri;
            }
            else if (AccessTokenUrl.IsPresent())
            {
                disco = await httpClient.GetDiscoveryDocumentAsync(AccessTokenUrl);
                if (!disco.IsError)
                {
                    AccessTokenUrl = disco.TokenEndpoint;
                    AuthorizeUrl = disco.AuthorizeEndpoint;
                    JwkSetUrl = disco.JwksUri;
                }
                else if (AuthorizeUrl.IsPresent())
                {
                    disco = await httpClient.GetDiscoveryDocumentAsync(AuthorizeUrl);
                    if (!disco.IsError)
                    {
                        AccessTokenUrl = disco.TokenEndpoint;
                        AuthorizeUrl = disco.AuthorizeEndpoint;
                        JwkSetUrl = disco.JwksUri;
                    }
                }
                else if (JwkSetUrl.IsPresent())
                {
                    disco = await httpClient.GetDiscoveryDocumentAsync(JwkSetUrl);
                    if (!disco.IsError)
                    {
                        AccessTokenUrl = disco.TokenEndpoint;
                        AuthorizeUrl = disco.AuthorizeEndpoint;
                        JwkSetUrl = disco.JwksUri;
                    }
                }
            }
        }

        public void UpdateEntity(Platform platform)
        {
            platform.AccessTokenUrl = AccessTokenUrl;
            platform.AuthorizeUrl = AuthorizeUrl;
            platform.Name = Name;
            platform.Issuer = Issuer;
            platform.JwkSetUrl = JwkSetUrl;

            platform.ClientId = ClientId;
            platform.PrivateKey = PrivateKey;
        }
    }
}
