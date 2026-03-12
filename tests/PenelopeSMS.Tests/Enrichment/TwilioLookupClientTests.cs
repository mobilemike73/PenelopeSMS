using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.Twilio;
using Twilio.Exceptions;
using Twilio.Rest.Lookups.V2;

namespace PenelopeSMS.Tests.Enrichment;

public sealed class TwilioLookupClientTests
{
    [Fact]
    public async Task LookupAsyncMapsValidationAndLineTypeFields()
    {
        const string payload = """
            {
              "phone_number": "+16502530000",
              "country_code": "US",
              "valid": true,
              "validation_errors": [],
              "line_type_intelligence": {
                "type": "mobile",
                "carrier_name": "Twilio Wireless",
                "mobile_country_code": "310",
                "mobile_network_code": "260"
              }
            }
            """;

        var client = new TwilioLookupClient((phoneNumber, _) =>
        {
            Assert.Equal("+16502530000", phoneNumber);
            return Task.FromResult(PhoneNumberResource.FromJson(payload));
        });

        var result = await client.LookupAsync("+16502530000");

        Assert.True(result.IsSuccess);
        Assert.Equal("+16502530000", result.LookupPhoneNumber);
        Assert.True(result.IsValid);
        Assert.Equal("US", result.CountryCode);
        Assert.Equal("mobile", result.LineType);
        Assert.Equal("Twilio Wireless", result.CarrierName);
        Assert.Equal("310", result.MobileCountryCode);
        Assert.Equal("260", result.MobileNetworkCode);
        Assert.Empty(result.ValidationErrors);
        Assert.Equal(CampaignEligibilityStatus.Eligible, result.EligibilityStatus);
        Assert.Equal(EnrichmentFailureStatus.None, result.FailureStatus);
        Assert.NotNull(result.RawPayloadJson);
        Assert.Contains("\"line_type_intelligence\"", result.RawPayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LookupAsyncMarksClientApiErrorsAsPermanentFailures()
    {
        var client = new TwilioLookupClient((_, _) => Task.FromException<PhoneNumberResource>(
            new ApiException(
                code: 20404,
                status: 400,
                message: "The requested resource was not found.",
                moreInfo: string.Empty,
                details: new Dictionary<string, object>(),
                exception: null)));

        var result = await client.LookupAsync("+16502530000");

        Assert.False(result.IsSuccess);
        Assert.Equal(EnrichmentFailureStatus.Permanent, result.FailureStatus);
        Assert.Equal(CampaignEligibilityStatus.Ineligible, result.EligibilityStatus);
        Assert.Equal("20404", result.ErrorCode);
    }

    [Fact]
    public async Task LookupAsyncMarksServerApiErrorsAsRetryableFailures()
    {
        var client = new TwilioLookupClient((_, _) => Task.FromException<PhoneNumberResource>(
            new ApiException(
                code: 20500,
                status: 503,
                message: "Twilio is temporarily unavailable.",
                moreInfo: string.Empty,
                details: new Dictionary<string, object>(),
                exception: null)));

        var result = await client.LookupAsync("+16502530000");

        Assert.False(result.IsSuccess);
        Assert.Equal(EnrichmentFailureStatus.Retryable, result.FailureStatus);
        Assert.Equal(CampaignEligibilityStatus.Ineligible, result.EligibilityStatus);
        Assert.Equal("20500", result.ErrorCode);
    }

    [Fact]
    public async Task LookupAsyncMarksTransportFailuresAsRetryable()
    {
        var client = new TwilioLookupClient((_, _) => Task.FromException<PhoneNumberResource>(
            new HttpRequestException("Connection reset by peer.")));

        var result = await client.LookupAsync("+16502530000");

        Assert.False(result.IsSuccess);
        Assert.Equal(EnrichmentFailureStatus.Retryable, result.FailureStatus);
        Assert.Equal("lookup_transport_failure", result.ErrorCode);
    }
}
