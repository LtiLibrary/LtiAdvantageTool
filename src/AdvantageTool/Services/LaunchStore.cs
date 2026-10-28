using System.IdentityModel.Tokens.Jwt;
using IdentityModel;
using Microsoft.Extensions.Caching.Memory;

namespace AdvantageTool.Services;

/// <summary>A validated launch, kept on the server so later requests don't trust a re-posted id_token.</summary>
public sealed record Launch(string Id, string PlatformId, string MessageType, JwtHeader Header, JwtPayload Payload);

/// <summary>
/// Keeps validated launches for a while. The pages only pass the opaque launch id
/// around, so a user cannot post a forged token to the AGS/NRPS handlers.
/// </summary>
public class LaunchStore(IMemoryCache cache)
{
    public Launch Save(string platformId, string messageType, JwtHeader header, JwtPayload payload)
    {
        var launch = new Launch(CryptoRandom.CreateUniqueId(), platformId, messageType, header, payload);
        cache.Set(Key(launch.Id), launch, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(1) });
        return launch;
    }

    public Launch? Get(string? id) =>
        !string.IsNullOrEmpty(id) && cache.TryGetValue<Launch>(Key(id), out var launch) ? launch : null;

    private static string Key(string id) => $"launch:{id}";
}
