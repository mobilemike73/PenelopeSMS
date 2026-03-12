namespace PenelopeSMS.Domain.Entities;

public sealed class CustomerPhoneLink
{
    public int Id { get; set; }

    public string CustSid { get; set; } = string.Empty;

    public string RawPhoneNumber { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastImportedAtUtc { get; set; } = DateTime.UtcNow;

    public int PhoneNumberRecordId { get; set; }

    public PhoneNumberRecord PhoneNumberRecord { get; set; } = null!;

    public int ImportBatchId { get; set; }

    public ImportBatch ImportBatch { get; set; } = null!;
}
