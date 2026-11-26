namespace AWL.Infrastructure.Ipc;

/// <summary>
/// The per-user single-instance lock (SAG-05). Hold it for the life of the process and dispose it on the thread
/// that acquired it.
/// </summary>
public sealed class SingleInstanceLock : IDisposable
{
    private readonly Mutex _mutex;

    private SingleInstanceLock(Mutex mutex) => _mutex = mutex;

    /// <summary>Returns the lock, or null when another AWL process of this user already holds it.</summary>
    public static SingleInstanceLock? TryAcquire(string? name = null)
    {
        var mutex = new Mutex(initiallyOwned: false, name ?? InstanceNames.Mutex);
        try
        {
            if (mutex.WaitOne(TimeSpan.Zero))
            {
                return new SingleInstanceLock(mutex);
            }
        }
        catch (AbandonedMutexException)
        {
            // A previous AWL ended without releasing the lock; this process owns it now.
            return new SingleInstanceLock(mutex);
        }
        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
