namespace AdvantageTool.Data
{
    public class Client
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ClientId { get; set; }
        public string PlatformIssuer { get; set; }
        public string PlatformAccessTokenUrl { get; set; }
        public string PlatformJsonWebKeysUrl { get; set; }
        public string UserId { get; set; }
    }
}
