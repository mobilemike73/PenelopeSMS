using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using PenelopeSMS.CallbackBridge.Functions;
using PenelopeSMS.CallbackBridge.Models;
using PenelopeSMS.CallbackBridge.Services;

namespace PenelopeSMS.Tests.Delivery;

public sealed class CallbackBridgeTests
{
    [Fact]
    public async Task HandleAsyncPublishesDeliveryEnvelopeForValidSignature()
    {
        TwilioCallbackEnvelope? publishedEnvelope = null;
        var function = new DeliveryStatusCallbackFunction(
            new TwilioWebhookValidator((_, _, _) => true),
            new SqsCallbackPublisher((envelope, _) =>
            {
                publishedEnvelope = envelope;
                return Task.CompletedTask;
            }));

        var response = await function.HandleAsync(
            CreateRequest("MessageSid=SM123&MessageStatus=delivered&ErrorCode=30001"),
            new TestLambdaContext());

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(publishedEnvelope);
        Assert.Equal("delivery", publishedEnvelope!.EnvelopeType);
        Assert.Equal("SM123", publishedEnvelope.MessageSid);
        Assert.Equal("delivered", publishedEnvelope.MessageStatus);
        Assert.Equal("30001", publishedEnvelope.ProviderErrorCode);
    }

    [Fact]
    public async Task HandleAsyncPublishesRejectedEnvelopeForInvalidSignature()
    {
        TwilioCallbackEnvelope? publishedEnvelope = null;
        var function = new DeliveryStatusCallbackFunction(
            new TwilioWebhookValidator((_, _, _) => false),
            new SqsCallbackPublisher((envelope, _) =>
            {
                publishedEnvelope = envelope;
                return Task.CompletedTask;
            }));

        var response = await function.HandleAsync(
            CreateRequest("MessageSid=SM123&MessageStatus=delivered"),
            new TestLambdaContext());

        Assert.Equal(403, response.StatusCode);
        Assert.NotNull(publishedEnvelope);
        Assert.Equal("rejected", publishedEnvelope!.EnvelopeType);
        Assert.Equal("invalid_signature", publishedEnvelope.RejectionReason);
    }

    private static APIGatewayHttpApiV2ProxyRequest CreateRequest(string body)
    {
        return new APIGatewayHttpApiV2ProxyRequest
        {
            Body = body,
            Headers = new Dictionary<string, string>
            {
                ["Host"] = "callbacks.example.com",
                ["X-Forwarded-Proto"] = "https",
                ["X-Twilio-Signature"] = "signature"
            },
            RawPath = "/twilio/status-callback",
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                {
                    Method = "POST",
                    Path = "/twilio/status-callback"
                }
            }
        };
    }

    private sealed class TestLambdaContext : ILambdaContext
    {
        public string AwsRequestId => Guid.NewGuid().ToString();

        public IClientContext ClientContext => null!;

        public string FunctionName => "callback-bridge";

        public string FunctionVersion => "1";

        public ICognitoIdentity Identity => null!;

        public string InvokedFunctionArn => "arn:aws:lambda:us-east-1:123456789012:function:callback-bridge";

        public ILambdaLogger Logger => new TestLambdaLogger();

        public string LogGroupName => "callback-bridge";

        public string LogStreamName => "callback-bridge";

        public int MemoryLimitInMB => 512;

        public TimeSpan RemainingTime => TimeSpan.FromMinutes(1);

        private sealed class TestLambdaLogger : ILambdaLogger
        {
            public void Log(string message)
            {
            }

            public void LogLine(string message)
            {
            }
        }
    }
}
