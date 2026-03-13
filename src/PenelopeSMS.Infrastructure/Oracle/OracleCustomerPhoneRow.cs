using PenelopeSMS.Domain.Enums;

namespace PenelopeSMS.Infrastructure.Oracle;

public sealed record OracleCustomerPhoneRow(
    string CustSid,
    string PhoneNumber,
    bool IsVip,
    ImportedPhoneSource ImportedPhoneSource);
