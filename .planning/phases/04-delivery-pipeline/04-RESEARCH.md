# Phase 4: Delivery Pipeline - Research

**Researched:** 2026-03-12
**Domain:** Twilio status callbacks, AWS callback ingress, Amazon SQS consumption, and SQL-backed delivery history
**Confidence:** HIGH

## User Constraints

### Locked Decisions From Context

#### Delivery statuses and history detail
- Treat `queued`, `sent`, `delivered`, `undelivered`, and `failed` as the first-class SMS delivery states for v1.
- Store all callback details on each persisted history entry: Twilio event timestamp, app processed timestamp, provider error code, provider error message, and a raw payload snapshot.
- Collapse repeated callbacks when nothing meaningful changed instead of storing identical duplicates.
- Determine the current message status from the latest Twilio event time, not simply the latest callback arrival time.

#### Unknown or invalid callback handling
- Callbacks that reference an unknown Twilio Message SID go into an unmatched-events bucket.
- Malformed payloads fail that event only; processing continues for the rest of the queue.
- Signature/authenticity failures are handled separately from malformed payloads.
- Invalid or unknown events should raise console warnings in v1.

#### Duplicate and out-of-order event behavior
- When a duplicate callback arrives with the same status and no meaningful new details, update a `last seen` timestamp on the collapsed entry.
- Discard older events once a newer event has already been applied.
- Current message status still follows the latest Twilio event time.
- Late non-terminal callbacks such as `sent` should not be kept after a terminal outcome has already been recorded.

#### Callback-to-queue flow expectations
- Use a simple sequential worker for v1 queue processing.
- Failed SQS processing should move toward a dead-letter path.
- Callback processing should run continuously while the app is open.
- Console output for callback processing should be verbose.

#### Claude's Discretion
- Exact schema shape for unmatched-event storage, collapsed-history bookkeeping, and callback-history tables.
- Exact console wording and progress formatting, as long as warnings and verbose processing output remain visible.
- Exact retry/dead-letter thresholds and queue-poll timing, as long as the dead-letter path is the default failure direction.

#### Deferred Ideas

None - discussion stayed within phase scope.

## Summary

Phase 4 should use an AWS-native public ingress, not a publicly hosted endpoint inside the local console app. Twilio requires a publicly accessible status callback URL, sends `application/x-www-form-urlencoded` POST requests, warns that webhook parameters can change without notice, and signs each request with `X-Twilio-Signature`. API Gateway with Lambda proxy integration is the cleanest fit because it accepts the full incoming request without brittle mapping rules, lets a small bridge validate Twilio signatures and normalize the payload, and decouples public internet reachability from the Windows-only local app.

Inside the local app, the delivery processor should run as a continuous `BackgroundService` on the existing Generic Host and consume SQS with long polling. For v1, keep this worker sequential exactly as the context specifies. Use SQS long polling, a DLQ, and delete messages only after the SQL update is committed. The outbound ledger added in Phase 3 already gives the durable correlation key: `CampaignRecipient.TwilioMessageSid`.

The highest-risk part of this phase is not SQS polling; it is correctness under duplicated, malformed, and out-of-order callbacks. The data model should separate current message status from event history. Persist normalized delivery events plus the raw payload, collapse true duplicates by updating `LastSeen`, and quarantine unknown, malformed, and signature-invalid events separately for console visibility. The planner also needs to account for a Twilio payload limitation: SMS status callbacks do not expose one universal event timestamp for every status. `RawDlrDoneDate` is available for most `delivered` and `undelivered` SMS/MMS callbacks, but `queued`, `sent`, and `failed` do not publish an equivalent general-purpose timestamp field in the callback payload.

**Primary recommendation:** Use `Twilio StatusCallback -> API Gateway Lambda proxy -> SQS -> .NET BackgroundService -> SQL Server` and keep the local app’s worker sequential, idempotent, and event-time-aware.

## Standard Stack

### Core

| Library / Service | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Amazon API Gateway + AWS Lambda proxy integration | Current managed service | Public Twilio callback ingress | Twilio requires a public URL, and Lambda proxy integration passes the full evolving request to code without fragile mapping templates. |
| Twilio C# helper library | Existing repo package (`Twilio` 7.14.3) | Set `StatusCallback` on sends and validate incoming webhook signatures | Official Twilio SDK includes `RequestValidator`; Twilio explicitly recommends SDK-based signature validation instead of custom code. |
| AWS SDK for .NET V4 + `AWSSDK.SQS` | V4 | Send normalized callback envelopes into SQS and consume them in the console app | AWS SDK V4 is the current line; direct SQS APIs fit the app’s simple sequential worker and raw Twilio payload shape. |
| `Microsoft.Extensions.Hosting` / `BackgroundService` | Existing .NET 9 stack | Continuous callback-processing worker inside the console app | The repo already uses Generic Host, and Microsoft’s worker pattern is the standard way to run long-lived background processing in .NET. |
| `Microsoft.EntityFrameworkCore.SqlServer` | Existing repo version (`9.0.x`) | Persist current delivery state, history, and unmatched/rejected callback records | Keeps delivery state in the same local system of record as Phase 3 campaign sends. |

### Supporting

| Library / Tool | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `AWSSDK.Extensions.NETCore.Setup` | Current V4-compatible package | Register AWS clients from configuration/DI | Use when wiring `IAmazonSQS` into the existing host cleanly. |
| `Microsoft.EntityFrameworkCore.Sqlite` | Existing repo version (`9.0.10`) | Fast relational tests for idempotent callback application | Use for repository and worker tests that don’t require SQL Server-specific behavior. |
| xUnit | Existing repo version | Worker, repository, and invalid-event-path tests | Continue the current automated verification approach. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| API Gateway + Lambda proxy bridge | Small hosted ASP.NET Core bridge | Viable, but adds always-on public hosting responsibility outside the local console app and doesn’t improve the local processing model. |
| Lambda proxy bridge | API Gateway direct service integration to SQS | Fewer moving parts, but poor fit for Twilio’s evolving form parameters and signature validation requirement because validation still needs custom logic somewhere. |
| Direct `AWSSDK.SQS` polling in a small sequential worker | AWS Message Processing Framework for .NET V4 | Useful if queue processing grows, but for v1 raw Twilio callback envelopes and sequential handling are simpler with low-level SQS calls. |

## Architecture Patterns

### Pattern 1: AWS-Native Callback Bridge
**What:** Use a small public ingress layer of `API Gateway -> Lambda` to accept Twilio status callbacks, validate the signature, normalize the request, and enqueue a delivery event to SQS.
**When to use:** For all Twilio delivery callbacks in this phase.
**Why:** Twilio requires a public callback URL, sends form-encoded POSTs, and may add parameters without notice. API Gateway Lambda proxy integration preserves the full request while keeping the local Windows app off the public internet.

### Pattern 2: Message-Specific `StatusCallback` Configuration
**What:** Set `StatusCallback` on each outbound send request from the app, with the URL provided from configuration.
**When to use:** When sending campaign messages in Phase 3 and later.
**Why:** Twilio sends callbacks to a message-specific `StatusCallback` URL, and that URL overrides a Messaging Service callback if both are present. Keeping it in app configuration makes environments explicit and testable.

### Pattern 3: Separate Current State From Event History
**What:** Keep current delivery state on `CampaignRecipient`, but persist each accepted delivery event in a separate history table and track collapsed duplicate metadata such as `LastSeenAtUtc`.
**When to use:** For every matched callback event.
**Why:** The user wants both an auditable history and a derived current state driven by provider event time where available. Those are different concerns and should not be merged into a single mutable row.

### Pattern 4: Best-Available Provider Event Time
**What:** Persist a `ProviderEventAtUtc` field on each history entry using:
- `RawDlrDoneDate` when Twilio includes it for `delivered` or `undelivered`
- otherwise a best-available fallback such as callback receipt time, while marking the source of that timestamp
**When to use:** On every accepted callback.
**Why:** The user chose event-time-based state progression, but Twilio SMS callbacks do not expose a universal timestamp across all statuses. The planner needs an explicit fallback rule instead of assuming Twilio always sends one.

### Pattern 5: Sequential Long-Poll Worker
**What:** Run a hosted background worker that long-polls SQS, processes one message at a time, commits SQL changes, and deletes the SQS message only after success.
**When to use:** For the local callback processor in v1.
**Why:** It matches the user’s sequential-processing choice, aligns with the existing Generic Host, and keeps idempotency and console visibility easier to reason about.

### Pattern 6: Quarantine Tables for Invalid and Unmatched Events
**What:** Persist separate records for:
- unmatched callbacks where `MessageSid` is unknown
- malformed payloads
- signature/authenticity failures
**When to use:** On any callback that cannot be safely applied to a known `CampaignRecipient`.
**Why:** The user explicitly wants these paths treated separately and surfaced with console warnings, not silently ignored.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Twilio webhook signature validation | Custom HMAC/signature code | Twilio SDK `RequestValidator` | Twilio explicitly recommends SDK-based validation because webhook parameters evolve. |
| Public callback hosting inside the local console app | Ad hoc internet-exposed listener on the operator machine | API Gateway + Lambda bridge | The app is Windows-local and single-operator; public ingress belongs in managed infrastructure. |
| Queue polling with short manual sleeps and no visibility strategy | Timer loops with naive `ReceiveMessage` calls | SQS long polling plus visibility timeout management | AWS recommends long polling and careful visibility timeout handling to reduce empty receives and duplicate processing. |
| Delivery state based only on callback arrival order | “Last write wins” by receive time | Event-time-aware updates using Twilio event time | Twilio explicitly warns that status callbacks don’t always arrive in order. |
| Silent dropping of bad callbacks | Ignoring malformed, unsigned, or unmatched events | Quarantine persistence plus console warnings | The user wants these paths visible and reviewable. |

## Common Pitfalls

- **Assuming every callback has a provider timestamp:** Twilio SMS callbacks only document `RawDlrDoneDate` for most `delivered` and `undelivered` callbacks. `queued`, `sent`, and `failed` need an explicit fallback rule if the app wants one comparable “event time” field.
- **Hardcoding a fixed webhook parameter list:** Twilio may add parameters without notice. Signature validation and parsing must tolerate extra fields.
- **Skipping signature validation in the public bridge:** This weakens trust in the callback path and makes malformed traffic indistinguishable from real Twilio events.
- **Using the local console app as the public endpoint:** That creates operational fragility and breaks when the operator machine or app isn’t available.
- **Deleting SQS messages before the SQL transaction succeeds:** That loses events permanently on mid-processing failures.
- **Ignoring visibility timeout:** If processing runs longer than visibility, the same message can reappear and be processed twice.
- **Collapsing events too aggressively:** Only exact duplicates with no meaningful new details should collapse; otherwise you lose legitimate history.
- **Not persisting raw callback payloads:** This removes the easiest debugging evidence for carrier issues, Twilio field changes, and unmatched events.
- **Treating `queued` as a callback-created state:** Twilio does not send a status callback for the initial created status; the send response from Phase 3 remains the source of the initial outbound status.
- **Relying on callbacks as a perfect source of truth forever:** Twilio’s own best-practices guide recommends later reconciliation/polling for missed updates. That can stay out of v1 scope, but the schema should not block it.

## Code Examples

### Twilio send request with `StatusCallback`
Source: https://www.twilio.com/docs/messaging/api/message-resource

```csharp
var options = new CreateMessageOptions(new PhoneNumber(toPhoneNumber))
{
    MessagingServiceSid = messagingServiceSid,
    Body = body,
    StatusCallback = new Uri(statusCallbackUrl)
};

var message = await MessageResource.CreateAsync(options, client: client);
```

### Validate an incoming Twilio webhook request with the C# SDK
Source: https://www.twilio.com/docs/usage/tutorials/how-to-secure-your-csharp-aspnet-core-app-by-validating-incoming-twilio-requests

```csharp
var validator = new RequestValidator(twilioAuthToken);

var isValid = validator.Validate(
    requestUrl,
    formValues,
    twilioSignatureHeader);

if (!isValid)
{
    return Results.StatusCode(StatusCodes.Status403Forbidden);
}
```

### Sequential SQS long-poll receive loop in a hosted worker
Sources:
- https://learn.microsoft.com/en-us/dotnet/core/extensions/workers
- https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/ReceiveMessage.html
- https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/best-practices-setting-up-long-polling.html

```csharp
public sealed class DeliveryCallbackWorker(IAmazonSQS sqsClient) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = 1,
                WaitTimeSeconds = 20,
                VisibilityTimeout = 120
            }, stoppingToken);

            foreach (var message in response.Messages)
            {
                var applied = await ApplyDeliveryEventAsync(message, stoppingToken);

                if (applied)
                {
                    await sqsClient.DeleteMessageAsync(queueUrl, message.ReceiptHandle, stoppingToken);
                }
            }
        }
    }
}
```

### Best-available event-time-aware current-status update
Derived from the user constraints plus the documented callback fields.

```csharp
if (incomingEvent.ProviderEventAtUtc < recipient.CurrentStatusAtUtc)
{
    return ApplyResult.DiscardedAsOlder;
}

if (IsDuplicate(recipient, incomingEvent))
{
    existingHistory.LastSeenAtUtc = utcNow;
    return ApplyResult.CollapsedDuplicate;
}

recipient.CurrentStatus = incomingEvent.MessageStatus;
recipient.CurrentStatusAtUtc = incomingEvent.ProviderEventAtUtc;
history.Add(incomingEvent);
```

## Validation Architecture

- Keep using `xUnit` and SQLite-backed tests for callback application, duplicate collapse, out-of-order rejection, and unmatched/rejected event storage.
- Add bridge-level tests for:
  - valid Twilio signature
  - invalid signature
  - malformed form payload
  - unknown `MessageSid`
- Add worker-level tests for:
  - message deleted only after successful persistence
  - failed processing leaves the SQS message for redelivery
  - older events do not overwrite newer event-time state
  - duplicate callbacks update `LastSeenAtUtc` rather than creating duplicate history rows
- Add one manual end-to-end verification before phase sign-off:
  1. Send a test SMS with a live `StatusCallback` URL.
  2. Confirm Lambda receives and validates the callback.
  3. Confirm SQS receives the normalized event.
  4. Confirm the local app applies history and current state correctly in SQL Server.

## Sources

- Twilio Message Resource: https://www.twilio.com/docs/messaging/api/message-resource
- Track the Message Status of Outbound Messages: https://www.twilio.com/docs/messaging/guides/track-outbound-message-status
- Outbound Message Status in Status Callbacks: https://www.twilio.com/docs/messaging/guides/outbound-message-status-in-status-callbacks
- Twilio Webhooks Security: https://www.twilio.com/docs/usage/webhooks/webhooks-security
- Secure your C# / ASP.NET Core app by validating incoming Twilio requests: https://www.twilio.com/docs/usage/tutorials/how-to-secure-your-csharp-aspnet-core-app-by-validating-incoming-twilio-requests
- Best Practices for Messaging Delivery Status Logging: https://www.twilio.com/docs/messaging/guides/outbound-message-logging
- Choose an AWS Lambda integration tutorial: https://docs.aws.amazon.com/apigateway/latest/developerguide/getting-started-with-lambda-integration.html
- Lambda proxy integrations in API Gateway: https://docs.aws.amazon.com/apigateway/latest/developerguide/set-up-lambda-proxy-integrations.html
- Setting-up long polling in Amazon SQS: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/best-practices-setting-up-long-polling.html
- Processing messages in a timely manner in Amazon SQS: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/best-practices-processing-messages-timely-manner.html
- Using dead-letter queues in Amazon SQS: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-dead-letter-queues.html
- Receiving Amazon SQS messages with AWS SDK for .NET V4: https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/ReceiveMessage.html
- Worker Services in .NET: https://learn.microsoft.com/en-us/dotnet/core/extensions/workers

---
*Phase research completed: 2026-03-12*
