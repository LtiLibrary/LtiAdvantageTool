using System.ComponentModel.DataAnnotations;
using AdvantageTool.Data;

namespace AdvantageTool.Pages.Platforms
{
    public class ClientModel
    {
        public ClientModel(Client client, string jwkSetUrl)
        {
            Id = client.Id;
            ClientId = client.ClientId;
            JwkSetUrl = jwkSetUrl;
            KeyId = client.KeyId;
            PrivateKey = client.PrivateKey;
            PublicKey = client.PublicKey;
        }

        public int Id { get; set; }

        /// <summary>
        /// Tool's OpenID Client ID
        /// </summary>
        [Required]
        [Display(Name = "Client ID")]
        public string ClientId { get; set; }

        [Display(Name = "JWK Set Url", Description = "The platform can retrieve the tool's public keys using this endpoint.")]
        public string JwkSetUrl { get; set; }

        /// <summary>
        /// The Key ID (kid) for the private/public key pair.
        /// </summary>
        public string KeyId { get; set; }

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
    }
}
