using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Domain.Entities;

namespace Aegis.Application.Interfaces;

/// <summary>
/// Abstracts persona storage. Current implementation is in-memory;
/// a future EF Core implementation will read from the Personas table.
/// </summary>
public interface IPersonaRepository
{
    /// <summary>Returns the default persona to use when no specific persona is selected.</summary>
    Task<Persona> GetDefaultAsync(CancellationToken ct = default);

    /// <summary>Returns a persona by its ID, or null if not found.</summary>
    Task<Persona?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all enabled personas, ordered by Name.</summary>
    Task<IReadOnlyList<Persona>> GetAllEnabledAsync(CancellationToken ct = default);
}
