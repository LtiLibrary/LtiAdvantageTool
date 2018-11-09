namespace AdvantageTool.Data
{
    public class Platform
    {
        public int Id { get; set; }
        public string AccessTokenUrl { get; set; }
        public string Name { get; set; }
        public string ClientId { get; set; }
        public string ClientPrivateKey { get; set; }
        public string ClientSecret { get; set; }
        public string Issuer { get; set; }
        public string JsonWebKeysUrl { get; set; }
        public string UserId { get; set; }
    }
}
