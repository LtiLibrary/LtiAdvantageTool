using System.ComponentModel.DataAnnotations;
using AdvantageTool.Utility;

namespace AdvantageTool.Pages.Platforms
{
    public class PlatformModel
    {
        public int Id { get; set; }

        [NullableUrl]
        [Display(Name = "Access Token URL")]
        public string PlatformAccessTokenUrl { get; set; }

        [Required]
        [Display(Name = "Client ID")]
        public string ToolClientId { get; set; }

        [Display(Name = "Client Secret")]
        public string ToolClientSecret { get; set; }

        [Required]
        [Display(Name = "Issuer", Description = "This is the Issuer for all messages that originate from the Platform.")]
        public string PlatformIssuer { get; set; }

        [NullableUrl]
        [Display(Name = "JSON Web Keys URL")]
        public string PlatformJsonWebKeysUrl { get; set; }

        [Required]
        [Display(Name = "Name")]
        public string PlatformName { get; set; }
    }
}
