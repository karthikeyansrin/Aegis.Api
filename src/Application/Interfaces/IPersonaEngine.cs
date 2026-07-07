using System.Threading;
using System.Threading.Tasks;

namespace Aegis.Application.Interfaces;

public interface IPersonaEngine
{
    Task<string?> GenerateAgentReplyAsync(string sessionId, string userMessage, bool isScam, CancellationToken ct = default);
}