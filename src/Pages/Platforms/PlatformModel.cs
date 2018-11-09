using System.ComponentModel.DataAnnotations;
using AdvantageTool.Utility;

namespace AdvantageTool.Pages.Platforms
{
    public class PlatformModel
    {
        public int Id { get; set; }

        [NullableUrl]
        [Display(Name = "Access Token URL")]
        public string AccessTokenUrl { get; set; }

        [Required]
        [Display(Name = "Client ID")]
        public string ClientId { get; set; }

        [Display(Name = "Client Secret", Description = "This is the shared secret the tool will use for client credentials.")]
        public string ClientSecret { get; set; }

        [Display(Name = "Private Key", Description = "This is the private key the tool will use for client credentials.")]
        public string ClientPrivateKey { get; set; }

        [Required]
        [Display(Name = "Issuer", Description = "This is the Issuer for all messages that originate from the Platform.")]
        public string Issuer { get; set; }

        [NullableUrl]
        [Display(Name = "JSON Web Keys URL")]
        public string JsonWebKeysUrl { get; set; }

        [Required]
        [Display(Name = "Name")]
        public string Name { get; set; }
    }
}
