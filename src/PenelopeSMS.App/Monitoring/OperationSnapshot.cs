namespace PenelopeSMS.App.Monitoring;

public sealed record OperationSnapshot(
    IReadOnlyList<ActiveOperationSnapshot> ActiveJobs,
    IReadOnlyList<CompletedOperationSnapshot> CompletedJobs,
    IReadOnlyList<OperationWarningSnapshot> ActiveWarnings,
    IReadOnlyList<string> LiveDeliveryLines);

public sealed record ActiveOperationSnapshot(
    string JobId,
    OperationType OperationType,
    string Label,
    DateTime StartedAtUtc,
    string? ProgressDetail);

public sealed record CompletedOperationSnapshot(
    string JobId,
    OperationType OperationType,
    string Label,
    string Outcome,
    DateTime CompletedAtUtc,
    string Summary);

public sealed record OperationWarningSnapshot(
    string WarningId,
    OperationType OperationType,
    string Message,
    DateTime CreatedAtUtc,
    string? JobId);
