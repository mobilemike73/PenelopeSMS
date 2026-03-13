using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;

namespace PenelopeSMS.Infrastructure.Aws;

public sealed class AwsSqsClient : IAwsSqsClient, IDisposable
{
    private const int WaitTimeSeconds = 20;
    private const int VisibilityTimeoutSeconds = 120;
    private readonly AmazonSQSClient sqsClient;

    public AwsSqsClient(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var region = configuration["Aws:Region"] ?? "us-east-1";
        var accessKeyId = configuration["Aws:AccessKeyId"];
        var secretAccessKey = configuration["Aws:SecretAccessKey"];
        var regionEndpoint = RegionEndpoint.GetBySystemName(region);

        sqsClient = string.IsNullOrWhiteSpace(accessKeyId) || string.IsNullOrWhiteSpace(secretAccessKey)
            ? new AmazonSQSClient(regionEndpoint)
            : new AmazonSQSClient(new BasicAWSCredentials(accessKeyId, secretAccessKey), regionEndpoint);
    }

    internal AwsSqsClient(AmazonSQSClient sqsClient)
    {
        this.sqsClient = sqsClient;
    }

    public async Task<SqsQueueMessage?> ReceiveMessageAsync(
        string queueUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueUrl);

        var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = WaitTimeSeconds,
            VisibilityTimeout = VisibilityTimeoutSeconds
        }, cancellationToken);

        var message = response.Messages.SingleOrDefault();

        return message is null
            ? null
            : new SqsQueueMessage(
                message.MessageId,
                message.Body,
                message.ReceiptHandle);
    }

    public Task DeleteMessageAsync(
        string queueUrl,
        string receiptHandle,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptHandle);

        return sqsClient.DeleteMessageAsync(queueUrl, receiptHandle, cancellationToken);
    }

    public void Dispose()
    {
        sqsClient.Dispose();
    }
}
