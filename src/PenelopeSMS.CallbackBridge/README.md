# PenelopeSMS Callback Bridge

This Lambda receives Twilio SMS `StatusCallback` webhooks, validates `X-Twilio-Signature`, normalizes the payload, and publishes the callback envelope into SQS for the local PenelopeSMS worker.

## Prerequisites

1. Create or reuse an SQS queue for callbacks.
2. Make sure the Lambda execution role can:
   - write CloudWatch Logs
   - call `sqs:SendMessage` on the callback queue
3. Install the AWS Lambda .NET tooling if it is not already available:

```bash
dotnet tool install -g Amazon.Lambda.Tools
```

## Deploy The Lambda

From the repo root:

```bash
dotnet lambda deploy-function PenelopeSMS-DeliveryStatusCallback \
  --project-location src/PenelopeSMS.CallbackBridge
```

The project already includes these defaults in `aws-lambda-tools-defaults.json`:

- runtime: `dotnet8`
- package type: `Zip`
- handler: `PenelopeSMS.CallbackBridge::PenelopeSMS.CallbackBridge.Functions.DeliveryStatusCallbackFunction::HandleAsync`
- default region: `us-east-1`

## Lambda Environment Variables

Set these environment variables on the Lambda itself:

- `CALLBACK_QUEUE_URL`
- `TWILIO_AUTH_TOKEN`
- `AWS_REGION`

`CALLBACK_QUEUE_URL` and `TWILIO_AUTH_TOKEN` are required. `AWS_REGION` should match the queue region and usually matches the Lambda region.

Example values:

- `CALLBACK_QUEUE_URL=https://sqs.us-east-1.amazonaws.com/123456789012/penelopesms-callbacks`
- `TWILIO_AUTH_TOKEN=...`
- `AWS_REGION=us-east-1`

## API Gateway

You can reuse an existing API Gateway. Add a dedicated public route or path mapping such as `/penelopesms/twilio/status-callback` and integrate it with this Lambda by proxy.

Recommended shape:

- API type: HTTP API
- method: `POST`
- route: `/penelopesms/twilio/status-callback`
- integration: Lambda proxy to `PenelopeSMS-DeliveryStatusCallback`

Twilio must be able to reach the route without your normal app auth. Validate Twilio requests in the Lambda with `X-Twilio-Signature` instead of placing the route behind a standard API authorizer.

## Twilio

Configure the public callback URL in the console app via `Twilio:StatusCallbackUrl`. New sends will attach that URL on each outbound message rather than relying on Twilio Console webhook settings.

Example:

```json
{
  "Twilio": {
    "StatusCallbackUrl": "https://your-api-id.execute-api.us-east-1.amazonaws.com/penelopesms/twilio/status-callback"
  }
}
```

## Smoke Test

1. Deploy the Lambda.
2. Attach the API Gateway route.
3. Set `Twilio:StatusCallbackUrl` in the main app.
4. Send a campaign batch.
5. Confirm:
   - Twilio calls the public callback URL
   - the Lambda logs successful invocations in CloudWatch
   - callback envelopes land in the SQS queue
   - the local PenelopeSMS worker drains the queue and updates delivery state
