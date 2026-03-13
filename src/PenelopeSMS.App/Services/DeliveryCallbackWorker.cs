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
    private readonly TextWriter output;

    public DeliveryCallbackWorker(
        IAwsSqsClient awsSqsClient,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AwsOptions> awsOptions)
        : this(
            awsSqsClient,
            awsOptions,
            (message, cancellationToken) => ProcessWithScopedWorkflowAsync(serviceScopeFactory, message, cancellationToken),
            Console.Out)
    {
    }

    internal DeliveryCallbackWorker(
        IAwsSqsClient awsSqsClient,
        IOptions<AwsOptions> awsOptions,
        Func<SqsQueueMessage, CancellationToken, Task<DeliveryCallbackProcessingResult>> processMessageAsync,
        TextWriter output)
    {
        this.awsSqsClient = awsSqsClient;
        this.awsOptions = awsOptions;
        this.processMessageAsync = processMessageAsync;
        this.output = output;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueUrl = awsOptions.Value.CallbackQueueUrl;

        if (string.IsNullOrWhiteSpace(queueUrl))
        {
            output.WriteLine("Delivery callback worker disabled: Aws:CallbackQueueUrl is not configured.");
            return;
        }

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
            return;
        }

        var message = await awsSqsClient.ReceiveMessageAsync(queueUrl, stoppingToken);

        if (message is null)
        {
            return;
        }

        try
        {
            var result = await processMessageAsync(message, stoppingToken);
            output.WriteLine(result.ConsoleMessage);

            if (!result.ShouldDeleteMessage)
            {
                output.WriteLine($"Leaving queue message {message.MessageId} for redelivery.");
                return;
            }

            await awsSqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle, stoppingToken);
            output.WriteLine($"Deleted queue message {message.MessageId} after {result.Outcome}.");
        }
        catch (Exception exception)
        {
            output.WriteLine($"Warning: delivery callback processing failed for queue message {message.MessageId}: {exception.Message}");
        }
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
