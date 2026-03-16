namespace PenelopeSMS.App.Workflows;

public sealed record DeliveryCallbackProcessingResult(
    bool ShouldDeleteMessage,
    string Outcome,
    string ConsoleMessage,
    string? MessageStatus = null,
    string? FailureCode = null,
    string? FailureMessage = null);
