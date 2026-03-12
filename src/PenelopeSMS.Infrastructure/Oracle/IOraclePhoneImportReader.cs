namespace PenelopeSMS.Infrastructure.Oracle;

public interface IOraclePhoneImportReader
{
    IAsyncEnumerable<OracleCustomerPhoneRow> ReadRowsAsync(CancellationToken cancellationToken = default);
}
