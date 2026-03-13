using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using PenelopeSMS.Domain.Enums;
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
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (OracleException exception)
        {
            throw CreateOracleOperationException(
                "open Oracle connection",
                connection.DataSource,
                exception);
        }

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandType = CommandType.Text;
        command.CommandText = importQuery;
        command.CommandTimeout = commandTimeoutSeconds;

        DbDataReader reader;

        try
        {
            reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        }
        catch (OracleException exception)
        {
            throw CreateOracleOperationException(
                "execute Oracle import query",
                connection.DataSource,
                exception);
        }

        await using (reader)
        {
        var custSidOrdinal = reader.GetOrdinal("CUST_SID");
        var phone1Ordinal = TryGetOrdinal(reader, "PHONE1");
        var phone2Ordinal = TryGetOrdinal(reader, "PHONE2");
        var isVipOrdinal = reader.GetOrdinal("IS_VIP");

        if (phone1Ordinal is null && phone2Ordinal is null)
        {
            throw new InvalidOperationException(
                "Oracle import query must return PHONE1 and/or PHONE2 columns.");
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            var custSid = reader.IsDBNull(custSidOrdinal) ? string.Empty : reader.GetString(custSidOrdinal);
            var isVip = GetBooleanFlag(reader, isVipOrdinal);
            var phone1 = phone1Ordinal is null ? string.Empty : GetTrimmedString(reader, phone1Ordinal.Value);
            var phone2 = phone2Ordinal is null ? string.Empty : GetTrimmedString(reader, phone2Ordinal.Value);

            if (!string.IsNullOrWhiteSpace(phone1))
            {
                yield return new OracleCustomerPhoneRow(
                    custSid,
                    phone1,
                    isVip,
                    ImportedPhoneSource.Phone1);
            }

            if (!string.IsNullOrWhiteSpace(phone2))
            {
                yield return new OracleCustomerPhoneRow(
                    custSid,
                    phone2,
                    isVip,
                    ImportedPhoneSource.Phone2);
            }
        }
        }
    }

    private static int? TryGetOrdinal(DbDataReader record, string columnName)
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

    private static bool GetBooleanFlag(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return false;
        }

        var value = reader.GetValue(ordinal);

        return value switch
        {
            bool boolValue => boolValue,
            byte byteValue => byteValue != 0,
            short shortValue => shortValue != 0,
            int intValue => intValue != 0,
            long longValue => longValue != 0,
            decimal decimalValue => decimalValue != 0,
            string stringValue => string.Equals(stringValue.Trim(), "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(stringValue.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(stringValue.Trim(), "y", StringComparison.OrdinalIgnoreCase)
                || string.Equals(stringValue.Trim(), "yes", StringComparison.OrdinalIgnoreCase),
            _ => Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0
        };
    }

    private static InvalidOperationException CreateOracleOperationException(
        string operation,
        string? dataSource,
        OracleException exception)
    {
        var endpoint = string.IsNullOrWhiteSpace(dataSource) ? "<unknown>" : dataSource;
        return new InvalidOperationException(
            $"Failed to {operation} against '{endpoint}'. Oracle error {exception.Number}: {exception.Message}",
            exception);
    }
}
