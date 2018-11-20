﻿namespace AdvantageTool.Data
{
    public class Platform
    {
        /// <summary>
        /// Primary key for this platform configuration
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Platform's access token endpoint
        /// </summary>
        public string AccessTokenUrl { get; set; }

        /// <summary>
        /// Platform name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Tool's OpenID Client ID
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// Tool's private key in PEM format
        /// </summary>
        public string ClientPrivateKey { get; set; }

        /// <summary>
        /// Tool's public key in PEM format
        /// </summary>
        public string ClientPublicKey { get; set; }

        /// <summary>
        /// Platform's issuer
        /// </summary>
        public string Issuer { get; set; }

        /// <summary>
        /// Platform's JWKS endpoint
        /// </summary>
        public string JsonWebKeySetUrl { get; set; }

        /// <summary>
        /// The user that owns this platform configuration
        /// </summary>
        public string UserId { get; set; }
    }
}
