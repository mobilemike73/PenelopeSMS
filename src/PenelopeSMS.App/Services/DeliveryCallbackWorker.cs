using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PenelopeSMS.App.Options;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Infrastructure.Aws;

namespace PenelopeSMS.App.Services;

public sealed class DeliveryCallbackWorker : BackgroundService
{
    private readonly IAwsSqsClient awsSqsClient;
    private readonly IDeliveryCallbackProcessingWorkflow deliveryCallbackProcessingWorkflow;
    private readonly IOptions<AwsOptions> awsOptions;
    private readonly TextWriter output;

    public DeliveryCallbackWorker(
        IAwsSqsClient awsSqsClient,
        IDeliveryCallbackProcessingWorkflow deliveryCallbackProcessingWorkflow,
        IOptions<AwsOptions> awsOptions)
        : this(awsSqsClient, deliveryCallbackProcessingWorkflow, awsOptions, Console.Out)
    {
    }

    internal DeliveryCallbackWorker(
        IAwsSqsClient awsSqsClient,
        IDeliveryCallbackProcessingWorkflow deliveryCallbackProcessingWorkflow,
        IOptions<AwsOptions> awsOptions,
        TextWriter output)
    {
        this.awsSqsClient = awsSqsClient;
        this.deliveryCallbackProcessingWorkflow = deliveryCallbackProcessingWorkflow;
        this.awsOptions = awsOptions;
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
            var result = await deliveryCallbackProcessingWorkflow.ProcessAsync(message, stoppingToken);
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
}
