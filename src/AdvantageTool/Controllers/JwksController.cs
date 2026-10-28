using LtiAdvantage.AspNetCore.Jwks;
using LtiAdvantage.Jwks;

namespace AdvantageTool.Controllers;

/// <summary>
/// The tool's JWKS at /.well-known/jwks.json. The platform fetches it to verify JWTs the
/// tool signs: deep linking responses, client assertions at the token endpoint, and so on.
/// <see cref="JwksControllerBase"/> does the work; the keys come from <see cref="IJwksKeyStore"/>.
/// </summary>
public class JwksController(IJwksKeyStore keyStore, ILogger<JwksControllerBase> logger)
    : JwksControllerBase(keyStore, logger);
