using System.Diagnostics.CodeAnalysis;

namespace AWL.Core.Common;

/// <summary>Represents the outcome of an operation that returns a value.</summary>
/// <typeparam name="T">The type of the successful return value.</typeparam>
public sealed class Result<T>
{
    private readonly T _value;

    internal Result(T value, AppError? error)
    {
        _value = value;
        Error = error;
    }

    /// <summary>Gets the error when the operation failed.</summary>
    public AppError? Error { get; }

    /// <summary>Gets whether the operation succeeded.</summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>Gets the return value of a successful operation.</summary>
    /// <exception cref="InvalidOperationException">The operation failed.</exception>
    public T Value => IsSuccess ? _value : throw new InvalidOperationException("A failed result has no value.");
}
