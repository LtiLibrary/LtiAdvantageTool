using System.ComponentModel.DataAnnotations;
using AdvantageTool.Data;

namespace AdvantageTool.Pages.Clients
{
    public class ClientModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Client ID")]
        public string ClientId { get; set; }

        [Required]
        [Display(Name = "Name")]
        public string ClientName { get; set; }

        [Required]
        public string Issuer { get; set; }

        [NullableUrl]
        [Display(Name = "Access Token URL")]
        public string AccessTokenUrl { get; set; }

        [NullableUrl]
        [Display(Name = "JSON Web Keys URL")]
        public string JsonWebKeysUrl { get; set; }
    }
}
