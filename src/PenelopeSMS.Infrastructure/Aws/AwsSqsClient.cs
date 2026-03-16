using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
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
        var profile = configuration["Aws:Profile"];
        var accessKeyId = configuration["Aws:AccessKeyId"];
        var secretAccessKey = configuration["Aws:SecretAccessKey"];
        var regionEndpoint = RegionEndpoint.GetBySystemName(region);

        if (!string.IsNullOrWhiteSpace(accessKeyId) && !string.IsNullOrWhiteSpace(secretAccessKey))
        {
            sqsClient = new AmazonSQSClient(new BasicAWSCredentials(accessKeyId, secretAccessKey), regionEndpoint);
            return;
        }

        if (!string.IsNullOrWhiteSpace(profile))
        {
            var profileStore = new CredentialProfileStoreChain();

            if (!profileStore.TryGetAWSCredentials(profile, out var profileCredentials))
            {
                throw new InvalidOperationException($"AWS profile '{profile}' was not found.");
            }

            sqsClient = new AmazonSQSClient(profileCredentials, regionEndpoint);
            return;
        }

        sqsClient = new AmazonSQSClient(regionEndpoint);
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

        var message = response.Messages?.SingleOrDefault();

        return message is null
            ? null
            : new SqsQueueMessage(
                message.MessageId,
                message.Body,
                message.ReceiptHandle);
    }

    public async Task<SqsQueueDepthSnapshot> GetQueueDepthAsync(
        string queueUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueUrl);

        var response = await sqsClient.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = queueUrl,
            AttributeNames =
            [
                QueueAttributeName.ApproximateNumberOfMessages,
                QueueAttributeName.ApproximateNumberOfMessagesNotVisible
            ]
        }, cancellationToken);

        var visibleMessages = TryParseAttributeValue(
            response.Attributes,
            QueueAttributeName.ApproximateNumberOfMessages);
        var messagesInFlight = TryParseAttributeValue(
            response.Attributes,
            QueueAttributeName.ApproximateNumberOfMessagesNotVisible);

        return new SqsQueueDepthSnapshot(visibleMessages, messagesInFlight);
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

    private static int TryParseAttributeValue(
        IReadOnlyDictionary<string, string>? attributes,
        string attributeName)
    {
        if (attributes is not null
            && attributes.TryGetValue(attributeName, out var attributeValue)
            && int.TryParse(attributeValue, out var parsedAttributeValue))
        {
            return parsedAttributeValue;
        }

        return 0;
    }
}
