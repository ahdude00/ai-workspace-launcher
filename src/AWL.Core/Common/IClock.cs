namespace AWL.Core.Common;

/// <summary>Provides the current time.</summary>
public interface IClock
{
    /// <summary>Gets the current time in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
