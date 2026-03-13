using PenelopeSMS.Infrastructure.Twilio;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;

namespace PenelopeSMS.Tests.Campaigns;

public sealed class TwilioMessageSenderTests
{
    [Fact]
    public async Task SendAsyncMapsAcceptedResponsesIntoInternalResult()
    {
        const string payload = """
            {
              "sid": "SM123456789",
              "status": "accepted"
            }
            """;

        var sender = new TwilioMessageSender((options, _) =>
        {
            Assert.Equal("+16502530000", options.To.ToString());
            Assert.Equal("Hello from PenelopeSMS", options.Body);
            Assert.Equal("https://callbacks.example.com/twilio/status-callback", options.StatusCallback?.ToString());
            return Task.FromResult(MessageResource.FromJson(payload));
        }, "https://callbacks.example.com/twilio/status-callback");

        var result = await sender.SendAsync("+16502530000", "Hello from PenelopeSMS");

        Assert.True(result.IsSuccess);
        Assert.Equal("SM123456789", result.MessageSid);
        Assert.Equal("accepted", result.InitialStatus);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task SendAsyncMapsApiFailuresIntoProviderErrorFields()
    {
        var sender = new TwilioMessageSender((_, _) => Task.FromException<MessageResource>(
            new ApiException(
                code: 21610,
                status: 400,
                message: "Message cannot be sent to the destination.",
                moreInfo: string.Empty,
                details: new Dictionary<string, object>(),
                exception: null)));

        var result = await sender.SendAsync("+16502530000", "Hello from PenelopeSMS");

        Assert.False(result.IsSuccess);
        Assert.Equal("21610", result.ErrorCode);
        Assert.Equal("Message cannot be sent to the destination.", result.ErrorMessage);
        Assert.Null(result.MessageSid);
        Assert.Null(result.InitialStatus);
    }

    [Fact]
    public async Task SendAsyncFailsWhenStatusCallbackUrlIsInvalid()
    {
        var sender = new TwilioMessageSender(
            (_, _) => Task.FromResult(MessageResource.FromJson("""{"sid":"SM123","status":"queued"}""")),
            "not-a-uri");

        var result = await sender.SendAsync("+16502530000", "Hello from PenelopeSMS");

        Assert.False(result.IsSuccess);
        Assert.Equal("twilio_status_callback_url_invalid", result.ErrorCode);
        Assert.Equal("Twilio StatusCallbackUrl must be an absolute URI.", result.ErrorMessage);
    }
}
