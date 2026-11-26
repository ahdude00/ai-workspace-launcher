using System.Windows.Threading;

namespace AWL.Desktop.Services;

internal sealed class WpfUiDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    public void Post(Func<Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        // Exceptions surface through the dispatcher and reach the global exception handler.
        dispatcher.BeginInvoke(async () => await work().ConfigureAwait(true));
    }
}
