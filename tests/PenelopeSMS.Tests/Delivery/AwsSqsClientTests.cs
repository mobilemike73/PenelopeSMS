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

    private sealed class FakeAmazonSqsClient(ReceiveMessageResponse response)
        : AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1)
    {
        public override Task<ReceiveMessageResponse> ReceiveMessageAsync(
            ReceiveMessageRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(response);
        }
    }
}
