using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using PenelopeSMS.Infrastructure.Aws;

namespace PenelopeSMS.Tests.Delivery;

public sealed class AwsSqsClientTests
{
    [Fact]
    public async Task ReceiveMessageAsyncReturnsNullWhenSdkReturnsNullMessagesCollection()
    {
        using var sqsClient = new AwsSqsClient(new FakeAmazonSqsClient(new ReceiveMessageResponse
        {
            Messages = null
        }));

        var message = await sqsClient.ReceiveMessageAsync("https://sqs.example.com/queue");

        Assert.Null(message);
    }

    [Fact]
    public async Task GetQueueDepthAsyncReturnsParsedAttributes()
    {
        using var sqsClient = new AwsSqsClient(new FakeAmazonSqsClient(
            receiveMessageResponse: new ReceiveMessageResponse
            {
                Messages = null
            },
            queueAttributesResponse: new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    [QueueAttributeName.ApproximateNumberOfMessages] = "123",
                    [QueueAttributeName.ApproximateNumberOfMessagesNotVisible] = "7"
                }
            }));

        var depth = await sqsClient.GetQueueDepthAsync("https://sqs.example.com/queue");

        Assert.Equal(123, depth.VisibleMessages);
        Assert.Equal(7, depth.MessagesInFlight);
    }

    private sealed class FakeAmazonSqsClient(
        ReceiveMessageResponse receiveMessageResponse,
        GetQueueAttributesResponse? queueAttributesResponse = null)
        : AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1)
    {
        public override Task<ReceiveMessageResponse> ReceiveMessageAsync(
            ReceiveMessageRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(receiveMessageResponse);
        }

        public override Task<GetQueueAttributesResponse> GetQueueAttributesAsync(
            GetQueueAttributesRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(queueAttributesResponse ?? new GetQueueAttributesResponse());
        }
    }
}
