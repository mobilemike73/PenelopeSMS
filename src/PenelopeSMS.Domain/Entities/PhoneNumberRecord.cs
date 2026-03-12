namespace PenelopeSMS.Domain.Entities;

public sealed class PhoneNumberRecord
{
    public int Id { get; set; }

    public string CanonicalPhoneNumber { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastImportedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CustomerPhoneLink> CustomerPhoneLinks { get; } = new List<CustomerPhoneLink>();
}
