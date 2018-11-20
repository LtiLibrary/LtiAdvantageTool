using System.ComponentModel.DataAnnotations;

namespace AdvantageTool.Data
{
    public class Client
    {
        public int Id { get; set; }

        /// <summary>
        /// Tool's OpenID Client ID
        /// </summary>
        [Required]
        [Display(Name = "Client ID")]
        public string ClientId { get; set; }

        /// <summary>
        /// Tool's private key in PEM format
        /// </summary>
        [Required]
        [Display(Name = "Private Key", Description = "This is the private key the tool will use to sign client credentials.")]
        public string PrivateKey { get; set; }

        /// <summary>
        /// Tool's public key in PEM format
        /// </summary>
        [Required]
        [Display(Name = "Public Key", Description = "This is the public key the platform should use to validate client credentials.")]
        public string PublicKey { get; set; }

        public string UserId { get; set; }
        public AdvantageToolUser User { get; set; }
    }
}
