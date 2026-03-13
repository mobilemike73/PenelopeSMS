using Microsoft.Extensions.Options;
using PenelopeSMS.App.Options;
using PenelopeSMS.App.Services;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Infrastructure.Aws;

namespace PenelopeSMS.Tests.Delivery;

public sealed class DeliveryCallbackWorkerTests
{
    [Fact]
    public async Task ExecuteOnceDeletesMessageAfterSuccessfulProcessing()
    {
        var sqsClient = new FakeAwsSqsClient(
            new SqsQueueMessage("message-1", "{}", "receipt-1"));
        var worker = new DeliveryCallbackWorker(
            sqsClient,
            new FakeDeliveryCallbackProcessingWorkflow(new DeliveryCallbackProcessingResult(
                ShouldDeleteMessage: true,
                Outcome: "applied",
                ConsoleMessage: "applied")),
            Options.Create(new AwsOptions
            {
                CallbackQueueUrl = "https://sqs.example.com/queue"
            }),
            TextWriter.Null);

        await worker.ProcessSingleIterationAsync();

        Assert.Equal(1, sqsClient.DeleteCalls);
    }

    [Fact]
    public async Task ExecuteOnceLeavesMessageForRedeliveryWhenProcessingFails()
    {
        var sqsClient = new FakeAwsSqsClient(
            new SqsQueueMessage("message-1", "{}", "receipt-1"));
        var worker = new DeliveryCallbackWorker(
            sqsClient,
            new FakeDeliveryCallbackProcessingWorkflow(new DeliveryCallbackProcessingResult(
                ShouldDeleteMessage: false,
                Outcome: "failed",
                ConsoleMessage: "failed")),
            Options.Create(new AwsOptions
            {
                CallbackQueueUrl = "https://sqs.example.com/queue"
            }),
            TextWriter.Null);

        await worker.ProcessSingleIterationAsync();

        Assert.Equal(0, sqsClient.DeleteCalls);
    }

    [Fact]
    public async Task ExecuteOnceLeavesMessageForRedeliveryWhenWorkflowThrows()
    {
        var sqsClient = new FakeAwsSqsClient(
            new SqsQueueMessage("message-1", "{}", "receipt-1"));
        var worker = new DeliveryCallbackWorker(
            sqsClient,
            new ThrowingDeliveryCallbackProcessingWorkflow(),
            Options.Create(new AwsOptions
            {
                CallbackQueueUrl = "https://sqs.example.com/queue"
            }),
            TextWriter.Null);

        await worker.ProcessSingleIterationAsync();

        Assert.Equal(0, sqsClient.DeleteCalls);
    }

    private sealed class FakeAwsSqsClient(SqsQueueMessage? nextMessage) : IAwsSqsClient
    {
        public int DeleteCalls { get; private set; }

        public Task<SqsQueueMessage?> ReceiveMessageAsync(string queueUrl, CancellationToken cancellationToken = default)
        {
            var message = nextMessage;
            nextMessage = null;
            return Task.FromResult(message);
        }

        public Task DeleteMessageAsync(string queueUrl, string receiptHandle, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDeliveryCallbackProcessingWorkflow(
        DeliveryCallbackProcessingResult result) : IDeliveryCallbackProcessingWorkflow
    {
        public Task<DeliveryCallbackProcessingResult> ProcessAsync(
            SqsQueueMessage message,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingDeliveryCallbackProcessingWorkflow : IDeliveryCallbackProcessingWorkflow
    {
        public Task<DeliveryCallbackProcessingResult> ProcessAsync(
            SqsQueueMessage message,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("boom");
        }
    }
}
