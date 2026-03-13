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
    private const string InvalidStatusCallbackUrlErrorCode = "twilio_status_callback_url_invalid";
    private readonly string accountSid;
    private readonly string authToken;
    private readonly string messagingServiceSid;
    private readonly string statusCallbackUrl;
    private readonly Func<CreateMessageOptions, CancellationToken, Task<MessageResource>> createMessageAsync;

    public TwilioMessageSender(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        accountSid = configuration["Twilio:AccountSid"] ?? string.Empty;
        authToken = configuration["Twilio:AuthToken"] ?? string.Empty;
        messagingServiceSid = configuration["Twilio:MessagingServiceSid"] ?? string.Empty;
        statusCallbackUrl = configuration["Twilio:StatusCallbackUrl"] ?? string.Empty;
        createMessageAsync = CreateMessageAsync;
    }

    internal TwilioMessageSender(
        Func<CreateMessageOptions, CancellationToken, Task<MessageResource>> createMessageAsync,
        string statusCallbackUrl = "")
    {
        ArgumentNullException.ThrowIfNull(createMessageAsync);

        accountSid = string.Empty;
        authToken = string.Empty;
        messagingServiceSid = string.Empty;
        this.statusCallbackUrl = statusCallbackUrl;
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
            if (!TryBuildMessageOptions(toPhoneNumber, messageBody, out var options, out var invalidConfigurationResult))
            {
                return invalidConfigurationResult;
            }

            var resource = await createMessageAsync(options, cancellationToken);
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
        CreateMessageOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = new TwilioRestClient(accountSid, authToken, accountSid: accountSid);
        return MessageResource.CreateAsync(options, client: client);
    }

    private bool TryBuildMessageOptions(
        string toPhoneNumber,
        string messageBody,
        out CreateMessageOptions options,
        out TwilioSendResult invalidConfigurationResult)
    {
        options = new CreateMessageOptions(new PhoneNumber(toPhoneNumber))
        {
            MessagingServiceSid = messagingServiceSid,
            Body = messageBody
        };

        invalidConfigurationResult = default!;

        if (string.IsNullOrWhiteSpace(statusCallbackUrl))
        {
            return true;
        }

        if (!Uri.TryCreate(statusCallbackUrl, UriKind.Absolute, out var statusCallbackUri))
        {
            invalidConfigurationResult = TwilioSendResult.Failure(
                InvalidStatusCallbackUrlErrorCode,
                "Twilio StatusCallbackUrl must be an absolute URI.");
            return false;
        }

        options.StatusCallback = statusCallbackUri;
        return true;
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
