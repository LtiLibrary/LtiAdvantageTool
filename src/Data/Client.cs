using System.ComponentModel.DataAnnotations;

namespace AdvantageTool.Data
{
    public class Client
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Client ID")]
        public string ClientId { get; set; }

        [Required]
        [Display(Name = "Name")]
        public string ClientName { get; set; }

        [Required]
        [Display(Name = "Client Secret")]
        public string ClientSecret { get; set; }

        /// <summary>
        /// The ID of the AdvantagePlatformUser that owns this Client.
        /// </summary>
        public string UserId { get; set; }
    }
}
