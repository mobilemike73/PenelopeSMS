namespace PenelopeSMS.Infrastructure.Twilio;

public sealed record TwilioSendResult
{
    public bool IsSuccess { get; init; }

    public string? MessageSid { get; init; }

    public string? InitialStatus { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static TwilioSendResult Success(string? messageSid, string? initialStatus)
    {
        return new TwilioSendResult
        {
            IsSuccess = true,
            MessageSid = messageSid,
            InitialStatus = initialStatus
        };
    }

    public static TwilioSendResult Failure(string errorCode, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new TwilioSendResult
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
