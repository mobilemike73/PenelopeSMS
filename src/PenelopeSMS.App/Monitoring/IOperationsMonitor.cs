namespace PenelopeSMS.App.Monitoring;

public interface IOperationsMonitor
{
    string StartJob(
        OperationType operationType,
        string label,
        string? progressDetail = null,
        DateTime? startedAtUtc = null);

    void UpdateJob(
        string jobId,
        string? progressDetail);

    void CompleteJob(
        string jobId,
        string summary,
        DateTime? completedAtUtc = null);

    void FailJob(
        string jobId,
        string summary,
        DateTime? completedAtUtc = null);

    void Warn(
        OperationType operationType,
        string message,
        string? jobId = null,
        DateTime? createdAtUtc = null);

    void ResolveWarnings(
        string? jobId = null,
        OperationType? operationType = null);

    void RecordLiveDeliveryLine(
        string message,
        DateTime? createdAtUtc = null);

    OperationSnapshot GetSnapshot();
}
