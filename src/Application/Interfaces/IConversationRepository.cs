using System.Threading;
using System.Threading.Tasks;
using Aegis.Domain.Entities;

namespace Aegis.Application.Interfaces;

public interface IConversationRepository
{
    Task<ConversationSession> GetOrCreateSessionAsync(string sessionId, CancellationToken ct = default);
    Task<ConversationSession?> GetSessionAsync(string sessionId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}