using System.ComponentModel.DataAnnotations;

namespace AdvantageTool.Data
{
    public class Client
    {
        [Required]
        [Display(Name = "ID")]
        public string Id { get; set; }

        [Required]
        [Display(Name = "Name")]
        public string Name { get; set; }

        [Required]
        [PrivateKey]
        [Display(Name = "Private Key")]
        public string PrivateKey { get; set; }

        [Required]
        [PublicKey]
        [Display(Name = "Public Key")]
        public string PublicKey { get; set; }

        /// <summary>
        /// The ID of the AdvantagePlatformUser that created this Client.
        /// </summary>
        public string UserId { get; set; }
    }
}
