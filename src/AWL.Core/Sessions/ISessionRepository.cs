namespace AWL.Core.Sessions;

/// <summary>Provides access to the stored AWL sessions.</summary>
public interface ISessionRepository
{
    /// <summary>Gets the session with the specified identifier, or null if it is absent.</summary>
    Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists sessions from newest to oldest, with ties ordered by identifier.</summary>
    Task<IReadOnlyList<Session>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores a new session.</summary>
    Task AddAsync(Session session, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing session and reports whether it was found.</summary>
    Task<bool> UpdateAsync(Session session, CancellationToken cancellationToken = default);

    /// <summary>Removes a session and reports whether it was found.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
