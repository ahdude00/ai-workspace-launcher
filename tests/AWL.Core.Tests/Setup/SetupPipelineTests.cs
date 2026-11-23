using AWL.Core.Common;
using AWL.Core.Configuration;
using AWL.Core.Setup;
using NSubstitute;

namespace AWL.Core.Tests.Setup;

public sealed class SetupPipelineTests
{
    [Fact]
    public async Task Retry_skips_verified_steps_and_repairs_a_failed_step()
    {
        var settings = Substitute.For<IAwlSettingsStore>();
        var first = Step("first");
        var second = Step("second");
        var last = Step("last");
        first.CheckAsync(default).Returns(false, true);
        second.RunAsync(default).Returns(Result.Fail(new AppError("blocked", "blocked")), Result.Ok());
        var pipeline = new SetupPipeline([first, second, last], settings);
        var summary = new List<string>();

        var updates = new List<(string, SetupProgress)>();
        (await pipeline.RunAsync(summary.Add, (id, state) => updates.Add((id, state)), default)).IsSuccess.ShouldBeFalse();
        Assert.Equal([("first", SetupProgress.Running), ("first", SetupProgress.Completed),
            ("second", SetupProgress.Running), ("second", SetupProgress.Failed)], updates);
        settings.Received().SetStepStatus("second", SetupStepStatus.Failed);
        await last.DidNotReceive().RunAsync(default);

        (await pipeline.RunAsync(summary.Add, default)).IsSuccess.ShouldBeTrue();
        await first.Received(1).RunAsync(default);
        await second.Received(2).RunAsync(default);
        settings.Received().SetStepStatus("second", SetupStepStatus.Completed);
        summary.ShouldContain("first: güncel");
    }

    [Fact]
    public async Task Cancellation_does_not_mark_an_interrupted_step_as_complete()
    {
        using var stop = new CancellationTokenSource();
        var settings = Substitute.For<IAwlSettingsStore>();
        var step = Step("python");
        step.RunAsync(stop.Token).Returns(_ =>
        {
            stop.Cancel();
            return Task.FromResult(Result.Ok());
        });
        var pipeline = new SetupPipeline([step], settings);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pipeline.RunAsync(_ => { }, stop.Token));
        settings.DidNotReceiveWithAnyArgs().SetStepStatus(default!, default);
    }

    private static ISetupStep Step(string id)
    {
        var step = Substitute.For<ISetupStep>();
        step.Id.Returns(id);
        step.RunAsync(default).Returns(Result.Ok());
        return step;
    }
}
