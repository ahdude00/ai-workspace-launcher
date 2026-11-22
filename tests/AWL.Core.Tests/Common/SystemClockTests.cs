using AWL.Core.Common;

namespace AWL.Core.Tests.Common;

public sealed class SystemClockTests
{
    [Fact]
    public void UtcNow_returns_the_current_utc_time()
    {
        var clock = new SystemClock();

        DateTimeOffset actual = clock.UtcNow;

        actual.Offset.ShouldBe(TimeSpan.Zero);
        (actual - DateTimeOffset.UtcNow).Duration().ShouldBeLessThan(TimeSpan.FromSeconds(10));
    }
}
