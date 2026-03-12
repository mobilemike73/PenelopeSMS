namespace PenelopeSMS.Domain.Entities;

public sealed class ImportBatch
{
    public const string InProgressStatus = "InProgress";
    public const string CompletedStatus = "Completed";
    public const string FailedStatus = "Failed";

    public int Id { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public int RowsRead { get; set; }

    public int RowsImported { get; set; }

    public int RowsRejected { get; set; }

    public string Status { get; set; } = InProgressStatus;

    public ICollection<CustomerPhoneLink> CustomerPhoneLinks { get; } = new List<CustomerPhoneLink>();
}
