using System.Globalization;
using Microsoft.Extensions.Configuration;
using PenelopeSMS.Domain.Enums;
using Twilio.Clients;
using Twilio.Exceptions;
using Twilio.Rest.Lookups.V2;

namespace PenelopeSMS.Infrastructure.Twilio;

public sealed class TwilioLookupClient : ITwilioLookupClient
{
    private const string LookupFieldList = "validation,line_type_intelligence";
    private const string MissingCredentialsErrorCode = "twilio_credentials_missing";
    private readonly string accountSid;
    private readonly string authToken;
    private readonly Func<string, CancellationToken, Task<PhoneNumberResource>> fetchPhoneNumberAsync;

    public TwilioLookupClient(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        accountSid = configuration["Twilio:AccountSid"] ?? string.Empty;
        authToken = configuration["Twilio:AuthToken"] ?? string.Empty;
        fetchPhoneNumberAsync = FetchPhoneNumberAsync;
    }

    internal TwilioLookupClient(Func<string, CancellationToken, Task<PhoneNumberResource>> fetchPhoneNumberAsync)
    {
        ArgumentNullException.ThrowIfNull(fetchPhoneNumberAsync);

        accountSid = string.Empty;
        authToken = string.Empty;
        this.fetchPhoneNumberAsync = fetchPhoneNumberAsync;
    }

    public async Task<TwilioLookupResult> LookupAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        cancellationToken.ThrowIfCancellationRequested();

        if (MissingCredentials())
        {
            return TwilioLookupResult.Failure(
                EnrichmentFailureStatus.Permanent,
                MissingCredentialsErrorCode,
                "Twilio Lookup credentials are not configured.");
        }

        try
        {
            var resource = await fetchPhoneNumberAsync(phoneNumber, cancellationToken);
            return MapResource(resource);
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
        return fetchPhoneNumberAsync == FetchPhoneNumberAsync
            && (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken));
    }

    private Task<PhoneNumberResource> FetchPhoneNumberAsync(
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var client = new TwilioRestClient(accountSid, authToken, accountSid: accountSid);
        var options = new FetchPhoneNumberOptions(phoneNumber)
        {
            Fields = LookupFieldList
        };

        return PhoneNumberResource.FetchAsync(options, client);
    }

    private static TwilioLookupResult MapResource(PhoneNumberResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var countryCode = NormalizeUpper(resource.CountryCode);
        var lineType = NormalizeLower(resource.LineTypeIntelligence?.Type);
        var validationErrors = resource.ValidationErrors?
            .Select(validationError => validationError?.ToString())
            .Where(validationError => !string.IsNullOrWhiteSpace(validationError))
            .Select(validationError => validationError!)
            .ToArray() ?? Array.Empty<string>();

        return TwilioLookupResult.Success(
            lookupPhoneNumber: resource.PhoneNumber?.ToString(),
            isValid: resource.Valid,
            validationErrors: validationErrors,
            countryCode: countryCode,
            lineType: lineType,
            carrierName: NullIfWhiteSpace(resource.LineTypeIntelligence?.CarrierName),
            mobileCountryCode: NormalizeUpper(resource.LineTypeIntelligence?.MobileCountryCode),
            mobileNetworkCode: NullIfWhiteSpace(resource.LineTypeIntelligence?.MobileNetworkCode),
            rawPayloadJson: PhoneNumberResource.ToJson(resource));
    }

    private static TwilioLookupResult MapFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is ApiException apiException)
        {
            return TwilioLookupResult.Failure(
                ClassifyFailure(apiException),
                apiException.Code.ToString(CultureInfo.InvariantCulture),
                apiException.Message);
        }

        return TwilioLookupResult.Failure(
            EnrichmentFailureStatus.Retryable,
            "lookup_transport_failure",
            exception.Message);
    }

    private static EnrichmentFailureStatus ClassifyFailure(ApiException exception)
    {
        var statusCode = Convert.ToInt32(exception.Status, CultureInfo.InvariantCulture);

        return statusCode switch
        {
            408 => EnrichmentFailureStatus.Retryable,
            429 => EnrichmentFailureStatus.Retryable,
            >= 500 and <= 599 => EnrichmentFailureStatus.Retryable,
            _ => EnrichmentFailureStatus.Permanent
        };
    }

    private static string? NormalizeUpper(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
    }

    private static string? NormalizeLower(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
