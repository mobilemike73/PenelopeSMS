using System.Globalization;
using Microsoft.Extensions.Configuration;
using Twilio.Clients;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace PenelopeSMS.Infrastructure.Twilio;

public sealed class TwilioMessageSender : ITwilioMessageSender
{
    private const string MissingCredentialsErrorCode = "twilio_messaging_credentials_missing";
    private readonly string accountSid;
    private readonly string authToken;
    private readonly string messagingServiceSid;
    private readonly Func<string, string, CancellationToken, Task<MessageResource>> createMessageAsync;

    public TwilioMessageSender(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        accountSid = configuration["Twilio:AccountSid"] ?? string.Empty;
        authToken = configuration["Twilio:AuthToken"] ?? string.Empty;
        messagingServiceSid = configuration["Twilio:MessagingServiceSid"] ?? string.Empty;
        createMessageAsync = CreateMessageAsync;
    }

    internal TwilioMessageSender(Func<string, string, CancellationToken, Task<MessageResource>> createMessageAsync)
    {
        ArgumentNullException.ThrowIfNull(createMessageAsync);

        accountSid = string.Empty;
        authToken = string.Empty;
        messagingServiceSid = string.Empty;
        this.createMessageAsync = createMessageAsync;
    }

    public async Task<TwilioSendResult> SendAsync(
        string toPhoneNumber,
        string messageBody,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toPhoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageBody);
        cancellationToken.ThrowIfCancellationRequested();

        if (MissingCredentials())
        {
            return TwilioSendResult.Failure(
                MissingCredentialsErrorCode,
                "Twilio messaging credentials are not configured.");
        }

        try
        {
            var resource = await createMessageAsync(toPhoneNumber, messageBody, cancellationToken);
            return TwilioSendResult.Success(
                resource.Sid,
                NormalizeLower(resource.Status?.ToString()));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return MapFailure(exception);
        }
    }

    private bool MissingCredentials()
    {
        return createMessageAsync == CreateMessageAsync
            && (string.IsNullOrWhiteSpace(accountSid)
                || string.IsNullOrWhiteSpace(authToken)
                || string.IsNullOrWhiteSpace(messagingServiceSid));
    }

    private Task<MessageResource> CreateMessageAsync(
        string toPhoneNumber,
        string messageBody,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = new TwilioRestClient(accountSid, authToken, accountSid: accountSid);
        var options = new CreateMessageOptions(new PhoneNumber(toPhoneNumber))
        {
            MessagingServiceSid = messagingServiceSid,
            Body = messageBody
        };

        return MessageResource.CreateAsync(options, client: client);
    }

    private static TwilioSendResult MapFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is ApiException apiException)
        {
            return TwilioSendResult.Failure(
                apiException.Code.ToString(CultureInfo.InvariantCulture),
                apiException.Message);
        }

        return TwilioSendResult.Failure(
            "message_transport_failure",
            exception.Message);
    }

    private static string? NormalizeLower(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }
}
