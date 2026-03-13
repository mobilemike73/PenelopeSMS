# Phase 4 User Setup

## AWS

1. Create or reuse an SQS queue for validated callback envelopes.
2. Attach a dead-letter queue to that callback queue.
3. Deploy `PenelopeSMS.CallbackBridge` as a Lambda.
4. Connect the Lambda to a public API Gateway route.

Recommended route:
- Reuse your existing API Gateway with a dedicated route or path mapping such as `/penelopesms/twilio/status-callback`.

Required Lambda configuration:
- `Aws__CallbackQueueUrl`: primary callback queue URL
- `Twilio__AuthToken`: Twilio auth token used for `X-Twilio-Signature` validation

## Twilio

Set the PenelopeSMS app configuration value:
- `Twilio:StatusCallbackUrl=https://your-public-domain/penelopesms/twilio/status-callback`

The app attaches this URL on every outbound SMS. Do not rely on Messaging Service webhook defaults for this phase.

## Local App

Set these values in `src/PenelopeSMS.App/appsettings.json` or user secrets:
- `Aws:CallbackQueueUrl`
- `Aws:CallbackDeadLetterQueueUrl`
- `Aws:Region`
- `Twilio:StatusCallbackUrl`

When the console host starts, the delivery callback worker starts automatically and runs continuously in the background while the app is open.

## Live Verification

1. Start the PenelopeSMS console app with the callback queue configured.
2. Send a campaign batch from the Campaigns menu.
3. Confirm the console reports that delivery callbacks are enabled.
4. Wait for Twilio callbacks to arrive through API Gateway and Lambda into SQS.
5. Confirm the console logs processed callback messages and SQL Server shows current delivery status plus history rows.

## Failure Path

- Invalid signatures and malformed callback payloads are persisted into `RejectedDeliveryCallbacks`.
- Unknown `MessageSid` callbacks are persisted into `UnmatchedDeliveryCallbacks`.
- Local processing failures leave the SQS message in the queue for retry and eventual DLQ routing.
