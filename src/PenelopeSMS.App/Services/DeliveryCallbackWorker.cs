using PenelopeSMS.App.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PenelopeSMS.App.Options;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Infrastructure.Aws;

namespace PenelopeSMS.App.Services;

public sealed class DeliveryCallbackWorker : BackgroundService
{
    private readonly IAwsSqsClient awsSqsClient;
    private readonly IOptions<AwsOptions> awsOptions;
    private readonly Func<SqsQueueMessage, CancellationToken, Task<DeliveryCallbackProcessingResult>> processMessageAsync;
    private readonly IOperationsMonitor operationsMonitor;
    private readonly TextWriter output;
    private string? activeJobId;
    private bool warnedMissingQueue;

    public DeliveryCallbackWorker(
        IAwsSqsClient awsSqsClient,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AwsOptions> awsOptions,
        IOperationsMonitor? runtimeOperationsMonitor = null)
        : this(
            awsSqsClient,
            awsOptions,
            (message, cancellationToken) => ProcessWithScopedWorkflowAsync(serviceScopeFactory, message, cancellationToken),
            runtimeOperationsMonitor ?? NullOperationsMonitor.Instance,
            Console.Out)
    {
    }

    internal DeliveryCallbackWorker(
        IAwsSqsClient awsSqsClient,
        IOptions<AwsOptions> awsOptions,
        Func<SqsQueueMessage, CancellationToken, Task<DeliveryCallbackProcessingResult>> processMessageAsync,
        IOperationsMonitor operationsMonitor,
        TextWriter output)
    {
        this.awsSqsClient = awsSqsClient;
        this.awsOptions = awsOptions;
        this.processMessageAsync = processMessageAsync;
        this.operationsMonitor = operationsMonitor;
        this.output = output;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueUrl = awsOptions.Value.CallbackQueueUrl;

        if (string.IsNullOrWhiteSpace(queueUrl))
        {
            output.WriteLine("Delivery callback worker disabled: Aws:CallbackQueueUrl is not configured.");
            WarnForMissingQueue();
            return;
        }

        EnsureJobStarted();

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessSingleIterationAsync(stoppingToken);
        }
    }

    internal async Task ProcessSingleIterationAsync(CancellationToken stoppingToken = default)
    {
        var queueUrl = awsOptions.Value.CallbackQueueUrl;

        if (string.IsNullOrWhiteSpace(queueUrl))
        {
            output.WriteLine("Delivery callback worker disabled: Aws:CallbackQueueUrl is not configured.");
            WarnForMissingQueue();
            return;
        }

        EnsureJobStarted();
        var message = await awsSqsClient.ReceiveMessageAsync(queueUrl, stoppingToken);

        if (message is null)
        {
            operationsMonitor.UpdateJob(activeJobId!, "Waiting for queue messages");
            return;
        }

        try
        {
            var result = await processMessageAsync(message, stoppingToken);
            output.WriteLine(result.ConsoleMessage);
            operationsMonitor.RecordLiveDeliveryLine(result.ConsoleMessage, DateTime.UtcNow);
            operationsMonitor.UpdateJob(activeJobId!, $"Last outcome: {result.Outcome}");
            operationsMonitor.ResolveWarnings(jobId: activeJobId);

            if (!result.ShouldDeleteMessage)
            {
                var redeliveryMessage = $"Leaving queue message {message.MessageId} for redelivery.";
                output.WriteLine(redeliveryMessage);
                operationsMonitor.RecordLiveDeliveryLine(redeliveryMessage, DateTime.UtcNow);
                return;
            }

            await awsSqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle, stoppingToken);
            var deletedMessage = $"Deleted queue message {message.MessageId} after {result.Outcome}.";
            output.WriteLine(deletedMessage);
            operationsMonitor.RecordLiveDeliveryLine(deletedMessage, DateTime.UtcNow);
        }
        catch (Exception exception)
        {
            var warningMessage = $"Warning: delivery callback processing failed for queue message {message.MessageId}: {exception.Message}";
            output.WriteLine(warningMessage);
            operationsMonitor.Warn(OperationType.DeliveryProcessing, warningMessage, activeJobId, DateTime.UtcNow);
            operationsMonitor.RecordLiveDeliveryLine(warningMessage, DateTime.UtcNow);
        }
    }

    private void EnsureJobStarted()
    {
        if (!string.IsNullOrWhiteSpace(activeJobId))
        {
            return;
        }

        activeJobId = operationsMonitor.StartJob(
            OperationType.DeliveryProcessing,
            "Delivery callback processing",
            "Waiting for queue messages");
        warnedMissingQueue = false;
    }

    private void WarnForMissingQueue()
    {
        if (warnedMissingQueue)
        {
            return;
        }

        operationsMonitor.Warn(
            OperationType.DeliveryProcessing,
            "Delivery callback worker disabled: Aws:CallbackQueueUrl is not configured.");
        warnedMissingQueue = true;
    }

    private static async Task<DeliveryCallbackProcessingResult> ProcessWithScopedWorkflowAsync(
        IServiceScopeFactory serviceScopeFactory,
        SqsQueueMessage message,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var workflow = scope.ServiceProvider.GetRequiredService<IDeliveryCallbackProcessingWorkflow>();
        return await workflow.ProcessAsync(message, cancellationToken);
    }
}
