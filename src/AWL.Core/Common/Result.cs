using System.Diagnostics.CodeAnalysis;

namespace AWL.Core.Common;

/// <summary>Represents the outcome of an operation without a return value.</summary>
/// <param name="Error">The error when the operation failed.</param>
public readonly record struct Result(AppError? Error)
{
    /// <summary>Gets whether the operation succeeded.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>Creates a successful result.</summary>
    public static Result Ok() => new(null);

    /// <summary>Creates a successful result with the given return value.</summary>
    public static Result<T> Ok<T>(T value) => new(value, null);

    /// <summary>Creates a failed result with the given error.</summary>
    public static Result Fail(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }

    /// <summary>Creates a failed result for an operation that returns a value.</summary>
    public static Result<T> Fail<T>(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(default!, error);
    }
}
