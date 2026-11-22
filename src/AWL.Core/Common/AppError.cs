namespace AWL.Core.Common;

/// <summary>Describes an expected operation failure.</summary>
/// <param name="Code">The stable error code.</param>
/// <param name="UserMessageKey">The resource key for the user-facing message.</param>
/// <param name="Detail">Optional diagnostic detail.</param>
public sealed record AppError(string Code, string UserMessageKey, string? Detail = null);
