using Twilio.Security;

namespace PenelopeSMS.CallbackBridge.Services;

public sealed class TwilioWebhookValidator
{
    private readonly string authToken;
    private readonly Func<Uri, IReadOnlyDictionary<string, string>, string?, bool> validate;

    public TwilioWebhookValidator()
    {
        authToken = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN") ?? string.Empty;
        validate = ValidateInternal;
    }

    internal TwilioWebhookValidator(
        Func<Uri, IReadOnlyDictionary<string, string>, string?, bool> validate)
    {
        ArgumentNullException.ThrowIfNull(validate);

        authToken = string.Empty;
        this.validate = validate;
    }

    public bool IsValid(
        Uri requestUri,
        IReadOnlyDictionary<string, string> formValues,
        string? signatureHeader)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        ArgumentNullException.ThrowIfNull(formValues);

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        return validate(requestUri, formValues, signatureHeader);
    }

    private bool ValidateInternal(
        Uri requestUri,
        IReadOnlyDictionary<string, string> formValues,
        string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(authToken))
        {
            return false;
        }

        var validator = new RequestValidator(authToken);
        return validator.Validate(
            requestUri.ToString(),
            formValues.ToDictionary(entry => entry.Key, entry => entry.Value),
            signatureHeader!);
    }
}
