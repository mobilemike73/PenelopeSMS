namespace PenelopeSMS.Infrastructure.Aws;

public interface IAwsSqsClient
{
    Task<SqsQueueMessage?> ReceiveMessageAsync(
        string queueUrl,
        CancellationToken cancellationToken = default);

    Task<SqsQueueDepthSnapshot> GetQueueDepthAsync(
        string queueUrl,
        CancellationToken cancellationToken = default);

    Task DeleteMessageAsync(
        string queueUrl,
        string receiptHandle,
        CancellationToken cancellationToken = default);
}

public sealed record SqsQueueMessage(
    string MessageId,
    string Body,
    string ReceiptHandle);

public sealed record SqsQueueDepthSnapshot(
    int VisibleMessages,
    int MessagesInFlight);
