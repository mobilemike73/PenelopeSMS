namespace PenelopeSMS.Infrastructure.Twilio;

public interface ITwilioLookupClient
{
    Task<TwilioLookupResult> LookupAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);
}
