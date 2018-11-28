namespace AdvantageTool.Data
{
    public class Platform
    {
        /// <summary>
        /// Primary key for this platform configuration
        /// </summary>
        public int Id { get; set; }

        #region Platform properties

        /// <summary>
        /// Platform's access token endpoint
        /// </summary>
        public string AccessTokenUrl { get; set; }

        /// <summary>
        /// Platform display name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Platform's issuer
        /// </summary>
        public string Issuer { get; set; }

        /// <summary>
        /// Platform's JWKS endpoint
        /// </summary>
        public string JwkSetUrl { get; set; }

        #endregion

        #region Tool Properties

        /// <summary>
        /// Tool's OpenID Client ID
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// The Key ID (kid) for the private/public key pair.
        /// </summary>
        public string KeyId { get; set; }

        /// <summary>
        /// Tool's private key in PEM format
        /// </summary>
        public string PrivateKey { get; set; }

        #endregion

        /// <summary>
        /// The user that created this platform registration.
        /// </summary>
        public AdvantageToolUser User { get; set; }
    }
}
