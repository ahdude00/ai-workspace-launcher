namespace AWL.Core.Shell;

/// <summary>Delivers launch requests forwarded by later AWL processes to the running one (SAG-05).</summary>
public interface ILaunchRequestSource
{
    /// <summary>Raised on a background thread with a request that was validated again in this process.</summary>
    event EventHandler<LaunchRequest>? RequestReceived;
}
