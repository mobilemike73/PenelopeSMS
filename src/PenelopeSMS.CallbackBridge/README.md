# PenelopeSMS Callback Bridge

This Lambda receives Twilio SMS `StatusCallback` webhooks, validates `X-Twilio-Signature`, normalizes the payload, and publishes the callback envelope into SQS for the local PenelopeSMS worker.

## Deploy

1. Create or reuse an SQS queue for callbacks and a DLQ for failed processing.
2. Set environment variables for the Lambda:
   - `Aws__CallbackQueueUrl`
   - `Twilio__AuthToken`
3. Deploy the function:

```bash
dotnet lambda deploy-function PenelopeSMS-DeliveryStatusCallback
```

## API Gateway

You can reuse an existing API Gateway. Add a dedicated public route or path mapping such as `/penelopesms/twilio/status-callback` and integrate it with this Lambda by proxy.

Twilio must be able to reach the route without your normal app auth. Validate Twilio requests in the Lambda with `X-Twilio-Signature` instead of placing the route behind a standard API authorizer.

## Twilio

Configure the public callback URL in the console app via `Twilio:StatusCallbackUrl`. New sends will attach that URL on each outbound message rather than relying on Twilio Console webhook settings.
