using System.Data;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace PenelopeSMS.Infrastructure.Oracle;

public sealed class OraclePhoneImportReader(IConfiguration configuration) : IOraclePhoneImportReader
{
    public async IAsyncEnumerable<OracleCustomerPhoneRow> ReadRowsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var connectionString = configuration["Oracle:ConnectionString"];
        var importQuery = configuration["Oracle:ImportQuery"];
        var commandTimeoutSeconds = int.TryParse(
            configuration["Oracle:CommandTimeoutSeconds"],
            out var parsedCommandTimeoutSeconds)
            ? parsedCommandTimeoutSeconds
            : 120;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Oracle connection string is not configured.");
        }

        if (string.IsNullOrWhiteSpace(importQuery))
        {
            throw new InvalidOperationException("Oracle import query is not configured.");
        }

        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandType = CommandType.Text;
        command.CommandText = importQuery;
        command.CommandTimeout = commandTimeoutSeconds;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        var custSidOrdinal = reader.GetOrdinal("CUST_SID");
        var phoneNumberOrdinal = reader.GetOrdinal("PHONE_NUMBER");

        while (await reader.ReadAsync(cancellationToken))
        {
            var custSid = reader.IsDBNull(custSidOrdinal) ? string.Empty : reader.GetString(custSidOrdinal);
            var phoneNumber = reader.IsDBNull(phoneNumberOrdinal) ? string.Empty : reader.GetString(phoneNumberOrdinal);

            yield return new OracleCustomerPhoneRow(custSid, phoneNumber);
        }
    }
}
