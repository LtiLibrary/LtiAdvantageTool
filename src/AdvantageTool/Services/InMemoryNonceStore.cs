using System.Collections.Concurrent;
using LtiAdvantage.IdentityModel.Validation;

namespace AdvantageTool.Services;

/// <summary>
/// Remembers the nonces of accepted id_tokens so a replayed token is rejected
/// (LTI 1.3 Security Framework §5.1.3, step 9). In-memory is fine for a single
/// instance; a tool running on several servers needs a shared store.
/// </summary>
public class InMemoryNonceStore : INonceStore
{
    private readonly ConcurrentDictionary<string, DateTime> _seen = new();

    public Task<bool> TryConsumeAsync(string nonce, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        // Entries only have to outlive the token (plus clock skew): after that the
        // validator's lifetime check rejects a replay anyway.
        var now = DateTime.UtcNow;
        foreach (var (key, until) in _seen)
        {
            if (until < now) _seen.TryRemove(key, out _);
        }

        // TryAdd is atomic, so two concurrent replays cannot both succeed.
        return Task.FromResult(_seen.TryAdd(nonce, expiresAt.AddMinutes(10)));
    }
}
