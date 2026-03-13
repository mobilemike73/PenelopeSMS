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
        var messageBody = System.Text.Json.JsonSerializer.Serialize(envelope);

        await sqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = messageBody
        }, cancellationToken);
    }
}
