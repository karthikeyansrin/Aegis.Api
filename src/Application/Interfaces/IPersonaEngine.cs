using System.Threading;
using System.Threading.Tasks;
using Aegis.Domain.Entities;

using Aegis.Domain.Enums;

namespace Aegis.Application.Interfaces;

public interface IPersonaEngine
{
    Task<string?> GenerateAgentReplyAsync(string sessionId, string userMessage, bool isScam, Persona persona, ConversationStage currentStage, CancellationToken ct = default);
}