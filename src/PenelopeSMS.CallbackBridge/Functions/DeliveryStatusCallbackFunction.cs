using System.Net;
using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using PenelopeSMS.CallbackBridge.Models;
using PenelopeSMS.CallbackBridge.Services;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace PenelopeSMS.CallbackBridge.Functions;

public sealed class DeliveryStatusCallbackFunction
{
    private const string SignatureHeaderName = "X-Twilio-Signature";
    private readonly SqsCallbackPublisher sqsCallbackPublisher;
    private readonly TwilioWebhookValidator twilioWebhookValidator;

    public DeliveryStatusCallbackFunction()
        : this(new TwilioWebhookValidator(), new SqsCallbackPublisher())
    {
    }

    internal DeliveryStatusCallbackFunction(
        TwilioWebhookValidator twilioWebhookValidator,
        SqsCallbackPublisher sqsCallbackPublisher)
    {
        this.twilioWebhookValidator = twilioWebhookValidator;
        this.sqsCallbackPublisher = sqsCallbackPublisher;
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(
        APIGatewayHttpApiV2ProxyRequest request,
        ILambdaContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var receivedAtUtc = DateTime.UtcNow;
        var body = ReadBody(request);
        var requestUri = BuildRequestUri(request);
        var signatureHeader = GetHeader(request.Headers, SignatureHeaderName);

        if (!TryParseForm(body, out var formValues, out var parseError))
        {
            var envelope = TwilioCallbackEnvelope.CreateRejected(
                rejectionReason: "malformed_payload",
                rawPayloadJson: SerializePayload(body, request.Headers),
                receivedAtUtc: receivedAtUtc,
                signatureHeader: signatureHeader,
                errorMessage: parseError);

            await sqsCallbackPublisher.PublishAsync(envelope, CancellationToken.None);
            return CreateResponse(HttpStatusCode.BadRequest, "Malformed callback payload.");
        }

        var isSignatureValid = twilioWebhookValidator.IsValid(
            requestUri,
            formValues,
            signatureHeader);

        if (!isSignatureValid)
        {
            var envelope = TwilioCallbackEnvelope.CreateRejected(
                rejectionReason: "invalid_signature",
                rawPayloadJson: SerializePayload(body, request.Headers),
                receivedAtUtc: receivedAtUtc,
                signatureHeader: signatureHeader,
                errorMessage: "Twilio signature validation failed.");

            await sqsCallbackPublisher.PublishAsync(envelope, CancellationToken.None);
            return CreateResponse(HttpStatusCode.Forbidden, "Invalid signature.");
        }

        var deliveryEnvelope = TwilioCallbackEnvelope.CreateDelivery(
            messageSid: ResolveMessageSid(formValues),
            messageStatus: ResolveValue(formValues, "MessageStatus"),
            providerErrorCode: ResolveValue(formValues, "ErrorCode"),
            providerErrorMessage: ResolveValue(formValues, "ErrorMessage"),
            providerEventRawValue: ResolveValue(formValues, "RawDlrDoneDate"),
            rawPayloadJson: SerializePayload(body, request.Headers),
            receivedAtUtc: receivedAtUtc,
            signatureHeader: signatureHeader);

        await sqsCallbackPublisher.PublishAsync(deliveryEnvelope, CancellationToken.None);
        return CreateResponse(HttpStatusCode.OK, "Accepted.");
    }

    private static bool TryParseForm(
        string body,
        out Dictionary<string, string> formValues,
        out string errorMessage)
    {
        formValues = new Dictionary<string, string>(StringComparer.Ordinal);
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(body))
        {
            errorMessage = "Callback body was empty.";
            return false;
        }

        try
        {
            foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separatorIndex = pair.IndexOf('=');

                if (separatorIndex <= 0)
                {
                    errorMessage = $"Malformed form field: {pair}";
                    return false;
                }

                var key = Uri.UnescapeDataString(pair[..separatorIndex].Replace('+', ' '));
                var value = Uri.UnescapeDataString(pair[(separatorIndex + 1)..].Replace('+', ' '));
                formValues[key] = value;
            }

            return true;
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
            return false;
        }
    }

    private static string ResolveMessageSid(IReadOnlyDictionary<string, string> formValues)
    {
        return ResolveValue(formValues, "MessageSid")
            ?? ResolveValue(formValues, "SmsSid")
            ?? string.Empty;
    }

    private static string? ResolveValue(
        IReadOnlyDictionary<string, string> formValues,
        string key)
    {
        return formValues.TryGetValue(key, out var value)
            ? value
            : null;
    }

    private static string ReadBody(APIGatewayHttpApiV2ProxyRequest request)
    {
        if (string.IsNullOrEmpty(request.Body))
        {
            return string.Empty;
        }

        if (!request.IsBase64Encoded)
        {
            return request.Body;
        }

        var bytes = Convert.FromBase64String(request.Body);
        return Encoding.UTF8.GetString(bytes);
    }

    private static Uri BuildRequestUri(APIGatewayHttpApiV2ProxyRequest request)
    {
        var requestContext = request.RequestContext.Http;
        var scheme = GetHeader(request.Headers, "X-Forwarded-Proto") ?? "https";
        var host = GetHeader(request.Headers, "Host") ?? "localhost";
        var path = request.RawPath ?? requestContext?.Path ?? "/";
        var queryString = string.IsNullOrWhiteSpace(request.RawQueryString)
            ? string.Empty
            : $"?{request.RawQueryString}";

        return new Uri($"{scheme}://{host}{path}{queryString}", UriKind.Absolute);
    }

    private static string? GetHeader(
        IDictionary<string, string>? headers,
        string headerName)
    {
        if (headers is null)
        {
            return null;
        }

        return headers.FirstOrDefault(
            header => string.Equals(header.Key, headerName, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static string SerializePayload(
        string body,
        IDictionary<string, string>? headers)
    {
        var payload = new
        {
            Body = body,
            Headers = headers ?? new Dictionary<string, string>()
        };

        return System.Text.Json.JsonSerializer.Serialize(payload);
    }

    private static APIGatewayHttpApiV2ProxyResponse CreateResponse(
        HttpStatusCode statusCode,
        string body)
    {
        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = (int)statusCode,
            Body = body
        };
    }
}
