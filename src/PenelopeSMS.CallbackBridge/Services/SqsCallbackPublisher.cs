using Amazon.SQS;
using Amazon.SQS.Model;
using PenelopeSMS.CallbackBridge.Models;

namespace PenelopeSMS.CallbackBridge.Services;

public sealed class SqsCallbackPublisher
{
    private readonly Func<TwilioCallbackEnvelope, CancellationToken, Task> publishAsync;

    public SqsCallbackPublisher()
    {
        publishAsync = PublishInternalAsync;
    }

    internal SqsCallbackPublisher(
        Func<TwilioCallbackEnvelope, CancellationToken, Task> publishAsync)
    {
        ArgumentNullException.ThrowIfNull(publishAsync);

        this.publishAsync = publishAsync;
    }

    public Task PublishAsync(
        TwilioCallbackEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return publishAsync(envelope, cancellationToken);
    }

    private static async Task PublishInternalAsync(
        TwilioCallbackEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var queueUrl = Environment.GetEnvironmentVariable("CALLBACK_QUEUE_URL") ?? string.Empty;
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";

        if (string.IsNullOrWhiteSpace(queueUrl))
        {
            throw new InvalidOperationException("CALLBACK_QUEUE_URL is not configured.");
        }

        using var sqsClient = new AmazonSQSClient(Amazon.RegionEndpoint.GetBySystemName(region));
        await sqsClient.SendMessageAsync(
            CreateSendMessageRequest(queueUrl, envelope),
            cancellationToken);
    }

    internal static SendMessageRequest CreateSendMessageRequest(
        string queueUrl,
        TwilioCallbackEnvelope envelope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueUrl);
        ArgumentNullException.ThrowIfNull(envelope);

        var request = new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = System.Text.Json.JsonSerializer.Serialize(envelope)
        };

        if (!queueUrl.EndsWith(".fifo", StringComparison.OrdinalIgnoreCase))
        {
            return request;
        }

        request.MessageGroupId = string.IsNullOrWhiteSpace(envelope.MessageSid)
            ? envelope.EnvelopeType
            : envelope.MessageSid;
        request.MessageDeduplicationId = Guid.NewGuid().ToString("N");
        return request;
    }
}
