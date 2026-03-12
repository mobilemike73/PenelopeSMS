namespace PenelopeSMS.App.Options;

public sealed class AwsOptions
{
    public const string SectionName = "Aws";

    public string Region { get; init; } = "us-east-1";

    public string AccessKeyId { get; init; } = string.Empty;

    public string SecretAccessKey { get; init; } = string.Empty;

    public string CallbackQueueUrl { get; init; } = string.Empty;
}
