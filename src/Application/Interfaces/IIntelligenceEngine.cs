using System.Threading;
using System.Threading.Tasks;
using Aegis.Domain.Entities;

namespace Aegis.Application.Interfaces;

public interface IIntelligenceEngine
{
    Task<ExtractedIntelligence> ExtractAsync(string sessionId, string latestMessage, bool fullContext = true, CancellationToken ct = default);
}