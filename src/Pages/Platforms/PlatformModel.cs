using System.ComponentModel.DataAnnotations;
using AdvantageTool.Data;
using AdvantageTool.Utility;
using LtiAdvantageLibrary.Utilities;

namespace AdvantageTool.Pages.Platforms
{
    /// <summary>
    /// Platform configuration.
    /// </summary>
    public class PlatformModel
    {
        public PlatformModel() {}

        public PlatformModel(Platform platform)
        {
            Id = platform.Id;
            AccessTokenUrl = platform.AccessTokenUrl;
            ClientId = platform.ClientId;
            ClientPrivateKey = platform.ClientPrivateKey;
            ClientPublicKey = RsaHelper.PublicKeyFromPrivateKey(platform.ClientPrivateKey);
            ClientSecret = platform.ClientSecret;
            Issuer = platform.Issuer;
            JsonWebKeySetUrl = platform.JsonWebKeySetUrl;
            Name = platform.Name;
        }

        public int Id { get; set; }

        [NullableUrl]
        [Display(Name = "Access Token URL")]
        public string AccessTokenUrl { get; set; }

        [Required]
        [Display(Name = "Client ID")]
        public string ClientId { get; set; }

        [Display(Name = "Private Key", Description = "<p>This is the private key the tool will use to sign client credentials.</p><p>Either a private/public key pair or a shared secret is required.</p>")]
        public string ClientPrivateKey { get; set; }

        [Display(Name = "Public Key", Description = "<p>Copy this to the platform to validate client credentials.</p><p>Either a private/public key pair or a shared secret is required.</p>")]
        public string ClientPublicKey { get; set; }

        [Display(Name = "Client Secret", Description = "<p>Copy this to the platform to validate client credentials.</p><p>Either a private/public key pair or a shared secret is required.</p>")]
        public string ClientSecret { get; set; }

        [Required]
        [Display(Name = "Issuer", Description = "This is the Issuer for all messages that originate from the Platform.")]
        public string Issuer { get; set; }

        [NullableUrl]
        [Display(Name = "JSON Web Key Set URL")]
        public string JsonWebKeySetUrl { get; set; }

        [Required]
        [Display(Name = "Name")]
        public string Name { get; set; }
    }
}
