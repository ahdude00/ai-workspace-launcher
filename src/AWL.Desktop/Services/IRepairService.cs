namespace AWL.Desktop.Services;

/// <summary>Runs every setup step again and completes what is missing (KUR-17).</summary>
public interface IRepairService
{
    /// <summary>Queues a repair on the UI thread; ignored while setup or a repair is already running.</summary>
    void RequestRepair();
}
