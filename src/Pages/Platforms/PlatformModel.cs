using System.ComponentModel.DataAnnotations;
using AdvantageTool.Utility;

namespace AdvantageTool.Pages.Platforms
{
    public class PlatformModel
    {
        public int Id { get; set; }

        [NullableUrl]
        [Display(Name = "Platform Access Token URL")]
        public string PlatformAccessTokenUrl { get; set; }

        [Required]
        [Display(Name = "Tool Client ID")]
        public string ToolClientId { get; set; }

        [Required]
        [Display(Name = "Platform Issuer", Description = "This is the Issuer for all messages that originate from the Platform.")]
        public string PlatformIssuer { get; set; }

        [NullableUrl]
        [Display(Name = "Platform JSON Web Keys URL")]
        public string PlatformJsonWebKeysUrl { get; set; }

        [Required]
        [Display(Name = "Platform Name")]
        public string PlatformName { get; set; }
    }
}
