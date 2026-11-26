namespace AWL.Desktop.Services;

/// <summary>Runs work on the UI thread so that view models stay free of WPF types.</summary>
public interface IUiDispatcher
{
    /// <summary>Queues the work on the UI thread without waiting for it.</summary>
    void Post(Func<Task> work);
}
