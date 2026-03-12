namespace PenelopeSMS.Infrastructure.Twilio;

public interface ITwilioMessageSender
{
    Task<TwilioSendResult> SendAsync(
        string toPhoneNumber,
        string messageBody,
        CancellationToken cancellationToken = default);
}
