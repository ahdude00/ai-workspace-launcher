using AWL.Core.Common;

namespace AWL.Core.Tests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Ok_returns_success_without_an_error()
    {
        Result result = Result.Ok();

        result.IsSuccess.ShouldBeTrue();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Fail_preserves_the_error()
    {
        var error = new AppError("test.failure", "TestFailure", "Diagnostic detail");

        Result result = Result.Fail(error);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeSameAs(error);
    }

    [Fact]
    public void Generic_ok_preserves_the_value()
    {
        Result<int> result = Result.Ok(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Generic_ok_accepts_null_for_a_nullable_type()
    {
        Result<string?> result = Result.Ok<string?>(null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Fact]
    public void Generic_fail_preserves_the_error_and_rejects_value_access()
    {
        var error = new AppError("test.failure", "TestFailure");

        Result<int> result = Result.Fail<int>(error);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeSameAs(error);
        Should.Throw<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void Fail_rejects_a_null_error()
    {
        Should.Throw<ArgumentNullException>(() => Result.Fail(null!));
        Should.Throw<ArgumentNullException>(() => Result.Fail<int>(null!));
    }
}
