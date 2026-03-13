namespace PenelopeSMS.App.Monitoring;

public sealed class OperationsMonitor : IOperationsMonitor
{
    private const int CompletedJobLimit = 10;
    private const int WarningLimit = 20;
    private const int LiveDeliveryLineLimit = 25;
    private readonly object gate = new();
    private readonly Dictionary<string, ActiveOperationSnapshot> activeJobs = [];
    private readonly List<CompletedOperationSnapshot> completedJobs = [];
    private readonly List<OperationWarningSnapshot> activeWarnings = [];
    private readonly Queue<string> liveDeliveryLines = [];

    public string StartJob(
        OperationType operationType,
        string label,
        string? progressDetail = null,
        DateTime? startedAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var jobId = Guid.NewGuid().ToString("N");
        var snapshot = new ActiveOperationSnapshot(
            jobId,
            operationType,
            label,
            startedAtUtc ?? DateTime.UtcNow,
            progressDetail);

        lock (gate)
        {
            activeJobs[jobId] = snapshot;
        }

        return jobId;
    }

    public void UpdateJob(
        string jobId,
        string? progressDetail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        lock (gate)
        {
            if (!activeJobs.TryGetValue(jobId, out var snapshot))
            {
                return;
            }

            activeJobs[jobId] = snapshot with
            {
                ProgressDetail = progressDetail
            };
        }
    }

    public void CompleteJob(
        string jobId,
        string summary,
        DateTime? completedAtUtc = null)
    {
        TransitionJob(jobId, "Completed", summary, completedAtUtc);
    }

    public void FailJob(
        string jobId,
        string summary,
        DateTime? completedAtUtc = null)
    {
        TransitionJob(jobId, "Failed", summary, completedAtUtc);
    }

    public void Warn(
        OperationType operationType,
        string message,
        string? jobId = null,
        DateTime? createdAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        lock (gate)
        {
            activeWarnings.Insert(0, new OperationWarningSnapshot(
                Guid.NewGuid().ToString("N"),
                operationType,
                message,
                createdAtUtc ?? DateTime.UtcNow,
                jobId));

            if (activeWarnings.Count > WarningLimit)
            {
                activeWarnings.RemoveRange(WarningLimit, activeWarnings.Count - WarningLimit);
            }
        }
    }

    public void ResolveWarnings(
        string? jobId = null,
        OperationType? operationType = null)
    {
        lock (gate)
        {
            activeWarnings.RemoveAll(warning =>
                (jobId is null || warning.JobId == jobId)
                && (!operationType.HasValue || warning.OperationType == operationType.Value));
        }
    }

    public void RecordLiveDeliveryLine(
        string message,
        DateTime? createdAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var line = createdAtUtc.HasValue
            ? $"[{createdAtUtc:HH:mm:ss}] {message}"
            : message;

        lock (gate)
        {
            liveDeliveryLines.Enqueue(line);

            while (liveDeliveryLines.Count > LiveDeliveryLineLimit)
            {
                liveDeliveryLines.Dequeue();
            }
        }
    }

    public OperationSnapshot GetSnapshot()
    {
        lock (gate)
        {
            return new OperationSnapshot(
                activeJobs.Values
                    .OrderBy(job => job.StartedAtUtc)
                    .ThenBy(job => job.JobId)
                    .ToList(),
                completedJobs.ToList(),
                activeWarnings
                    .OrderByDescending(warning => warning.CreatedAtUtc)
                    .ThenBy(warning => warning.WarningId)
                    .ToList(),
                liveDeliveryLines.ToList());
        }
    }

    private void TransitionJob(
        string jobId,
        string outcome,
        string summary,
        DateTime? completedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        lock (gate)
        {
            if (!activeJobs.Remove(jobId, out var snapshot))
            {
                return;
            }

            completedJobs.Insert(0, new CompletedOperationSnapshot(
                snapshot.JobId,
                snapshot.OperationType,
                snapshot.Label,
                outcome,
                completedAtUtc ?? DateTime.UtcNow,
                summary));

            if (completedJobs.Count > CompletedJobLimit)
            {
                completedJobs.RemoveRange(CompletedJobLimit, completedJobs.Count - CompletedJobLimit);
            }

            activeWarnings.RemoveAll(warning => warning.JobId == jobId);
        }
    }
}

internal sealed class NullOperationsMonitor : IOperationsMonitor
{
    public static NullOperationsMonitor Instance { get; } = new();

    public string StartJob(
        OperationType operationType,
        string label,
        string? progressDetail = null,
        DateTime? startedAtUtc = null)
    {
        return string.Empty;
    }

    public void UpdateJob(string jobId, string? progressDetail)
    {
    }

    public void CompleteJob(string jobId, string summary, DateTime? completedAtUtc = null)
    {
    }

    public void FailJob(string jobId, string summary, DateTime? completedAtUtc = null)
    {
    }

    public void Warn(
        OperationType operationType,
        string message,
        string? jobId = null,
        DateTime? createdAtUtc = null)
    {
    }

    public void ResolveWarnings(string? jobId = null, OperationType? operationType = null)
    {
    }

    public void RecordLiveDeliveryLine(string message, DateTime? createdAtUtc = null)
    {
    }

    public OperationSnapshot GetSnapshot()
    {
        return new OperationSnapshot([], [], [], []);
    }
}
