namespace AdvantageTool.Data
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
        /// Platform's issuer
        /// </summary>
        public string Issuer { get; set; }

        /// <summary>
        /// Platform's JWKS endpoint
        /// </summary>
        public string JsonWebKeySetUrl { get; set; }

        /// <summary>
        /// The user that created this platform registration.
        /// </summary>
        public AdvantageToolUser User { get; set; }
    }
}
