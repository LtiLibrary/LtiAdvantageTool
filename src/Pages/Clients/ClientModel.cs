using System.ComponentModel.DataAnnotations;
using AdvantageTool.Data;

namespace AdvantageTool.Pages.Clients
{
    public class ClientModel
    {
        public int Id { get; set; }

        [NullableUrl]
        [Display(Name = "Platform Access Token URL")]
        public string AccessTokenUrl { get; set; }

        [Required]
        [Display(Name = "Client ID")]
        public string ClientId { get; set; }

        [Required]
        [Display(Name = "Platform Issuer")]
        public string Issuer { get; set; }

        [NullableUrl]
        [Display(Name = "Platform JSON Web Keys URL")]
        public string JsonWebKeysUrl { get; set; }

        [Required]
        [Display(Name = "Name")]
        public string Name { get; set; }
    }
}
