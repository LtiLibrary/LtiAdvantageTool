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

    // The deployment ids the platform assigned to this registration, one per line.
    // LTI 1.3 §5.1.1: a tool must reject launches from deployments it does not know.
    public string DeploymentIds { get; set; } = string.Empty;

    public IReadOnlyList<string> DeploymentIdList => DeploymentIds
        .Split(['\n', '\r', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Tool side
    public string ClientId { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;

    // The key that was current before the last rotation. It stays published until it is
    // retired, so the platform can still verify tokens signed with it.
    public string? PreviousKeyId { get; set; }
    public string? PreviousPublicKey { get; set; }

    public string UserId { get; set; } = string.Empty;
    public AdvantageToolUser? User { get; set; }
}
