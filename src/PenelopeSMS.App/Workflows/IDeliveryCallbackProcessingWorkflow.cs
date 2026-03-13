using PenelopeSMS.Infrastructure.Aws;

namespace PenelopeSMS.App.Workflows;

public interface IDeliveryCallbackProcessingWorkflow
{
    Task<DeliveryCallbackProcessingResult> ProcessAsync(
        SqsQueueMessage message,
        CancellationToken cancellationToken = default);
}
