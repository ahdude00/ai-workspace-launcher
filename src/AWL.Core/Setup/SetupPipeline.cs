using AWL.Core.Common;
using AWL.Core.Configuration;

namespace AWL.Core.Setup;

public interface ISetupStep
{
    string Id { get; }
    Task<bool> CheckAsync(CancellationToken token);
    Task<Result> RunAsync(CancellationToken token);
}

public sealed class SetupPipeline(IEnumerable<ISetupStep> steps, IAwlSettingsStore settings)
{
    public async Task<Result> RunAsync(Action<string> report, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(report);
        foreach (var step in steps)
        {
            token.ThrowIfCancellationRequested();
            Result result;
            try
            {
                bool ready = await step.CheckAsync(token).ConfigureAwait(false);
                result = ready ? Result.Ok() : await step.RunAsync(token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (result.IsSuccess)
                {
                    settings.SetStepStatus(step.Id, SetupStepStatus.Completed);
                    report($"{step.Id}: {(ready ? "güncel" : "tamamlandı")}");
                    continue;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                result = Result.Fail(new AppError("SetupBlocked", "SetupBlocked", $"0x{ex.HResult:X8}"));
            }

            report($"{step.Id}: {result.Error!.Code} {result.Error.Detail}");
            try
            {
                settings.SetStepStatus(step.Id, SetupStepStatus.Failed);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                report($"settings: 0x{ex.HResult:X8}");
            }
            return result;
        }
        return Result.Ok();
    }
}
