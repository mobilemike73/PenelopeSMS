namespace PenelopeSMS.App.Options;

public sealed class OracleOptions
{
    public const string SectionName = "Oracle";

    public string ConnectionString { get; init; } = string.Empty;

    public string ImportQuery { get; init; } = string.Empty;

    public string DefaultRegion { get; init; } = "US";

    public int CommandTimeoutSeconds { get; init; } = 120;
}
