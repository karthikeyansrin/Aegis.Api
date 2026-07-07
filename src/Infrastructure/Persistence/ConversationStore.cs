using Aegis.Infrastructure.AI;
using Aegis.Infrastructure.Persistence;
using Aegis.Application.Services;
using Aegis.Application.Interfaces;
using System.Collections.Concurrent;
using Aegis.Application.DTOs;
using Aegis.Domain.Entities;

namespace Aegis.Infrastructure.Persistence;

/// <summary>
/// In-memory thread-safe conversation store with automatic expiry.
/// </summary>
public class ConversationStore : IConversationStore, IDisposable
{
    private readonly ConcurrentDictionary<string, ConversationSession> _sessions = new();
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _expiry;
    private bool _disposed;

    /// <summary>
    /// Create a conversation store.
    /// expiryWindow controls how long a session may be inactive before being removed.
    /// </summary>
    public ConversationStore(TimeSpan? expiryWindow = null)
    {
        _expiry = expiryWindow ?? TimeSpan.FromMinutes(45); // default in middle of 30-60 range

        // Run cleanup every 5 minutes
        _cleanupTimer = new Timer(_ => Cleanup(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public ConversationSession GetOrCreateSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentNullException(nameof(sessionId));

        return _sessions.GetOrAdd(sessionId, id => new ConversationSession(id));
    }

    public bool TryGetSession(string sessionId, out ConversationSession? session)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            session = null;
            return false;
        }

        return _sessions.TryGetValue(sessionId, out session);
    }

    public bool RemoveSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return false;
        return _sessions.TryRemove(sessionId, out _);
    }

    private void Cleanup()
    {
        try
        {
            var cutoff = DateTime.UtcNow - _expiry;
            foreach (var kv in _sessions)
            {
                try
                {
                    if (kv.Value.LastUpdatedUtc < cutoff)
                    {
                        _sessions.TryRemove(kv.Key, out _);
                    }
                }
                catch
                {
                    // swallow to keep cleanup robust
                }
            }
        }
        catch
        {
            // keep cleanup robust
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cleanupTimer.Dispose();
    }
}
