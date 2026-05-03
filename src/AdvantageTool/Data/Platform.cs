namespace AdvantageTool.Data;

public class Platform
{
    public int Id { get; set; }

    // Platform side (provided by the LMS)
    public string Name { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string AuthorizeUrl { get; set; } = string.Empty;
    public string AccessTokenUrl { get; set; } = string.Empty;
    public string JwkSetUrl { get; set; } = string.Empty;
    public string PlatformId { get; set; } = string.Empty;

    // Tool side
    public string ClientId { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;
    public AdvantageToolUser? User { get; set; }
}
