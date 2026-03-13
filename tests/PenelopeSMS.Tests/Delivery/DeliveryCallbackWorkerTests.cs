using Microsoft.Extensions.Options;
using PenelopeSMS.App.Monitoring;
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
            Options.Create(new AwsOptions
            {
                CallbackQueueUrl = "https://sqs.example.com/queue"
            }),
            (_, _) => Task.FromResult(new DeliveryCallbackProcessingResult(
                ShouldDeleteMessage: true,
                Outcome: "applied",
                ConsoleMessage: "applied")),
            new OperationsMonitor(),
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
            Options.Create(new AwsOptions
            {
                CallbackQueueUrl = "https://sqs.example.com/queue"
            }),
            (_, _) => Task.FromResult(new DeliveryCallbackProcessingResult(
                ShouldDeleteMessage: false,
                Outcome: "failed",
                ConsoleMessage: "failed")),
            new OperationsMonitor(),
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
            Options.Create(new AwsOptions
            {
                CallbackQueueUrl = "https://sqs.example.com/queue"
            }),
            (_, _) => throw new InvalidOperationException("boom"),
            new OperationsMonitor(),
            TextWriter.Null);

        await worker.ProcessSingleIterationAsync();

        Assert.Equal(0, sqsClient.DeleteCalls);
    }

    [Fact]
    public async Task ExecuteOnceHandlesReceiveFailuresWithoutThrowing()
    {
        var sqsClient = new ThrowingAwsSqsClient();
        var worker = new DeliveryCallbackWorker(
            sqsClient,
            Options.Create(new AwsOptions
            {
                CallbackQueueUrl = "https://sqs.example.com/queue"
            }),
            (_, _) => Task.FromResult(new DeliveryCallbackProcessingResult(
                ShouldDeleteMessage: true,
                Outcome: "applied",
                ConsoleMessage: "applied")),
            new OperationsMonitor(),
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

    private sealed class ThrowingAwsSqsClient : IAwsSqsClient
    {
        public int DeleteCalls { get; private set; }

        public Task<SqsQueueMessage?> ReceiveMessageAsync(string queueUrl, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("receive failed");
        }

        public Task DeleteMessageAsync(string queueUrl, string receiptHandle, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            return Task.CompletedTask;
        }
    }
}
