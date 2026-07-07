using Aegis.Domain.Entities;

namespace Aegis.Application.Interfaces;

public interface IConversationStore
{
    ConversationSession GetOrCreateSession(string sessionId);
    bool TryGetSession(string sessionId, out ConversationSession? session);
    bool RemoveSession(string sessionId);
}