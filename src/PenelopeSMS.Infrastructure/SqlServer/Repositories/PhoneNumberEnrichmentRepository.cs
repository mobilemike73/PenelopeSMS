using Microsoft.EntityFrameworkCore;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.Twilio;

namespace PenelopeSMS.Infrastructure.SqlServer.Repositories;

public sealed class PhoneNumberEnrichmentRepository(PenelopeSmsDbContext dbContext)
{
    public async Task ApplyResultAsync(
        int phoneNumberRecordId,
        TwilioLookupResult lookupResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lookupResult);

        var phoneNumberRecord = await dbContext.PhoneNumberRecords
            .SingleOrDefaultAsync(record => record.Id == phoneNumberRecordId, cancellationToken)
            ?? throw new InvalidOperationException($"Phone number record {phoneNumberRecordId} was not found.");

        var utcNow = DateTime.UtcNow;
        phoneNumberRecord.LastEnrichmentAttemptedAtUtc = utcNow;
        phoneNumberRecord.CampaignEligibilityStatus = lookupResult.EligibilityStatus;
        phoneNumberRecord.EligibilityEvaluatedAtUtc = utcNow;

        if (lookupResult.IsSuccess)
        {
            phoneNumberRecord.LastEnrichedAtUtc = utcNow;
            phoneNumberRecord.TwilioCountryCode = lookupResult.CountryCode;
            phoneNumberRecord.TwilioLineType = lookupResult.LineType;
            phoneNumberRecord.TwilioCarrierName = lookupResult.CarrierName;
            phoneNumberRecord.TwilioMobileCountryCode = lookupResult.MobileCountryCode;
            phoneNumberRecord.TwilioMobileNetworkCode = lookupResult.MobileNetworkCode;
            phoneNumberRecord.TwilioLookupPayloadJson = lookupResult.RawPayloadJson;
            phoneNumberRecord.EnrichmentFailureStatus = EnrichmentFailureStatus.None;
            phoneNumberRecord.LastEnrichmentFailedAtUtc = null;
            phoneNumberRecord.LastEnrichmentErrorCode = null;
            phoneNumberRecord.LastEnrichmentErrorMessage = null;
        }
        else
        {
            phoneNumberRecord.EnrichmentFailureStatus = lookupResult.FailureStatus;
            phoneNumberRecord.LastEnrichmentFailedAtUtc = utcNow;
            phoneNumberRecord.LastEnrichmentErrorCode = lookupResult.ErrorCode;
            phoneNumberRecord.LastEnrichmentErrorMessage = lookupResult.ErrorMessage;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
