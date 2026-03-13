using System.Data;
using System.Data.Common;
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
        var phone1Ordinal = TryGetOrdinal(reader, "PHONE1");
        var phone2Ordinal = TryGetOrdinal(reader, "PHONE2");

        if (phone1Ordinal is null && phone2Ordinal is null)
        {
            throw new InvalidOperationException(
                "Oracle import query must return PHONE1 and/or PHONE2 columns.");
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            var custSid = reader.IsDBNull(custSidOrdinal) ? string.Empty : reader.GetString(custSidOrdinal);
            var phone1 = phone1Ordinal is null ? string.Empty : GetTrimmedString(reader, phone1Ordinal.Value);
            var phone2 = phone2Ordinal is null ? string.Empty : GetTrimmedString(reader, phone2Ordinal.Value);

            if (!string.IsNullOrWhiteSpace(phone1))
            {
                yield return new OracleCustomerPhoneRow(custSid, phone1);
            }

            if (!string.IsNullOrWhiteSpace(phone2))
            {
                yield return new OracleCustomerPhoneRow(custSid, phone2);
            }
        }
    }

    private static int? TryGetOrdinal(IDataRecord record, string columnName)
    {
        for (var index = 0; index < record.FieldCount; index++)
        {
            if (string.Equals(record.GetName(index), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return null;
    }

    private static string GetTrimmedString(DbDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal).Trim();
    }
}
