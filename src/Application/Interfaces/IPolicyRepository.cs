using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Domain.Entities;

namespace Aegis.Application.Interfaces;

/// <summary>
/// Abstracts policy storage.
/// Current implementation is in-memory; a future EF Core implementation
/// will read from the Policies table in PostgreSQL.
/// </summary>
public interface IPolicyRepository
{
    /// <summary>Returns all enabled policies, ordered by Priority ascending.</summary>
    Task<IReadOnlyList<Policy>> GetEnabledPoliciesAsync(CancellationToken ct = default);
}
