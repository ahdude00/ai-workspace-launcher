namespace AWL.Core.Sessions;

/// <summary>Represents the metadata AWL stores for an individual session.</summary>
/// <param name="Id">The AWL session identifier.</param>
/// <param name="Title">The title chosen for the session.</param>
/// <param name="CategoryId">The category identifier, if any.</param>
/// <param name="WorkspacePath">The session's working directory.</param>
/// <param name="ClaudeSessionId">The identifier supplied to Claude Code.</param>
/// <param name="ClaudeVersion">The Claude Code version used to open the session, if known.</param>
/// <param name="IsArchived">Whether the session is archived.</param>
/// <param name="CreatedAt">When the session was created.</param>
/// <param name="LastOpenedAt">When the session was most recently opened, if ever.</param>
/// <param name="LastActivityAt">When the session was last active, if known.</param>
/// <param name="LastErrorCode">The latest session error code, if any.</param>
public sealed record Session(
    Guid Id,
    string Title,
    Guid? CategoryId,
    string WorkspacePath,
    Guid ClaudeSessionId,
    string? ClaudeVersion,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastOpenedAt,
    DateTimeOffset? LastActivityAt,
    string? LastErrorCode = null);
