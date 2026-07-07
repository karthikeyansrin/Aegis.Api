using System;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Application.Interfaces;
using Aegis.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aegis.Infrastructure.Persistence;

public class ConversationRepository : IConversationRepository
{
    private readonly AegisDbContext _context;

    public ConversationRepository(AegisDbContext context)
    {
        _context = context;
    }

    public async Task<ConversationSession> GetOrCreateSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var session = await GetSessionAsync(sessionId, ct);
        if (session == null)
        {
            session = new ConversationSession(sessionId);
            _context.Conversations.Add(session);
        }
        return session;
    }

    public async Task<ConversationSession?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        return await _context.Conversations
            .Include(c => c.History)
            .Include(c => c.AggregatedIntelligence)
                .ThenInclude(ai => ai.BankAccounts)
            .FirstOrDefaultAsync(c => c.SessionId == sessionId, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}