using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.Twilio;

namespace PenelopeSMS.Tests.Enrichment;

public sealed class PhoneEligibilityTests
{
    [Fact]
    public void DeriveEligibilityReturnsEligibleOnlyForValidUsMobileNumbers()
    {
        var result = TwilioLookupResult.DeriveEligibility("US", "mobile", isValid: true);

        Assert.Equal(CampaignEligibilityStatus.Eligible, result);
    }

    [Theory]
    [InlineData("CA", "mobile", true)]
    [InlineData("US", "voip", true)]
    [InlineData("US", "landline", true)]
    [InlineData("US", null, true)]
    [InlineData("US", "mobile", false)]
    [InlineData("US", "mobile", null)]
    public void DeriveEligibilityReturnsIneligibleForUnsupportedOrUnknownResults(
        string? countryCode,
        string? lineType,
        bool? isValid)
    {
        var result = TwilioLookupResult.DeriveEligibility(countryCode, lineType, isValid);

        Assert.Equal(CampaignEligibilityStatus.Ineligible, result);
    }
}
