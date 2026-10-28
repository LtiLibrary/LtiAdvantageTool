using System.Collections.Concurrent;
using IdentityModel;

namespace AdvantageTool.Services;

/// <summary>An attempt the tool is proctoring, and what the proctor has done to it so far.</summary>
public class ProctoringSession
{
    public string Id { get; } = CryptoRandom.CreateUniqueId(12);
    public required string PlatformId { get; init; }
    public required string Issuer { get; init; }
    public required string UserId { get; init; }
    public string? UserName { get; init; }
    public required string ResourceLinkId { get; init; }
    public string? ResourceLinkTitle { get; init; }
    public int AttemptNumber { get; init; }
    public string? AssessmentControlUrl { get; init; }
    public IReadOnlyList<string> Actions { get; init; } = [];
    public DateTime StartedAt { get; } = DateTime.UtcNow;
    public string Status { get; set; } = "started";
    public List<string> Log { get; } = [];
}

/// <summary>
/// The attempts in progress, in memory: enough for a sample. A real proctoring tool keeps
/// these in a database so the proctor's view survives a restart.
/// </summary>
public class ProctoringSessions
{
    private readonly ConcurrentDictionary<string, ProctoringSession> _sessions = new();

    public void Add(ProctoringSession session) => _sessions[session.Id] = session;

    public ProctoringSession? Get(string? id) => id is not null && _sessions.TryGetValue(id, out var s) ? s : null;

    public ProctoringSession? Find(string platformId, string userId, string resourceLinkId, int attempt) =>
        _sessions.Values.FirstOrDefault(s => s.PlatformId == platformId && s.UserId == userId
            && s.ResourceLinkId == resourceLinkId && s.AttemptNumber == attempt);

    public IReadOnlyList<ProctoringSession> All() => _sessions.Values.OrderByDescending(s => s.StartedAt).ToList();
}
