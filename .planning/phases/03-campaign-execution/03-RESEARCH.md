# Phase 3: Campaign Execution - Research

**Researched:** 2026-03-12
**Domain:** Campaign creation, recipient materialization, batched Twilio SMS sends, and outbound message ledger persistence
**Confidence:** HIGH

## Planning Assumptions

Phase 3 has no `03-CONTEXT.md`, so this plan is based on roadmap requirements, the completed Phase 2 implementation, and Twilio primary-source documentation.

- Campaigns are sent to canonical `PhoneNumberRecord` rows, not to every `CustomerPhoneLink`, because Phase 3 explicitly excludes duplicate sends to the same canonical phone number within a campaign.
- Templates remain plain text only in v1. No merge fields, personalization, or Twilio Content API work belongs in this phase.
- Delivery callbacks remain a Phase 4 concern. Phase 3 only needs to persist the initial Twilio response status and Message SID so later callback processing has durable correlation keys.
- Twilio sender provisioning is already complete outside the app, so sending should rely on the configured `MessagingServiceSid`.

## Summary

Phase 3 should split cleanly into three layers: a SQL Server campaign/message ledger foundation, a campaign creation workflow that snapshots a plain-text template file and materializes unique eligible recipients, and a send workflow that processes pending recipients in configurable batch sizes through Twilio. The highest-value design is to create one durable outbound row per campaign-recipient before any provider call is attempted. That gives the app a restart-safe ledger, enforces duplicate prevention inside a campaign, and gives Phase 4 a stable place to correlate callbacks later.

Twilio’s Message API already matches the needed transport shape. `CreateMessageOptions` supports `MessagingServiceSid`, `Body`, and optional `StatusCallback`, and `MessageResource.CreateAsync` returns the Twilio Message SID plus the provider’s initial status. That means Phase 3 does not need callback infrastructure to meet its own success criteria; it only needs to persist Twilio’s synchronous create response correctly and keep the callback URL question deferred.

The biggest design risk is mixing “campaign drafting” with “campaign sending” in one irreversible action. Campaign creation should first snapshot the template text and recipient set into local tables. Sending should then consume pending outbound rows in batches. That separation reduces duplicate-send risk, keeps tests deterministic, and makes later Phase 4 and Phase 5 monitoring work much easier.

**Primary recommendation:** Use a 3-plan sequence: first add campaign/message ledger schema and template-loading foundation, then implement campaign drafting and recipient materialization, then add Twilio batched sending with persisted provider response data.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Twilio .NET helper library | 7.14.3 | Send outbound SMS through the Twilio Message API | Already adopted in Phase 2 and provides the official .NET transport surface. |
| Microsoft.EntityFrameworkCore.SqlServer | 9.0.x | Persist campaigns, outbound recipients, and provider response fields | Existing system-of-record provider for the app. |
| Microsoft.EntityFrameworkCore.Sqlite | 9.0.10 | Fast relational tests for campaign drafting and send orchestration | Matches the existing workflow test strategy. |

### Supporting

| Library / Tool | Version | Purpose | When to Use |
|----------------|---------|---------|-------------|
| xUnit | existing project version | Workflow and persistence tests | Continue the current automated verification approach. |
| File I/O in .NET BCL | .NET 9 | Load plain-text templates from disk | Sufficient because templates are static text files only. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Snapshotting template body into a campaign row | Read the template file again at send time | Re-reading keeps storage smaller, but makes campaign history mutable and brittle if the file changes or disappears. |
| One outbound ledger row per canonical phone | Send directly from a transient query of eligible phones | Direct send is simpler initially, but removes restart safety, weakens duplicate protection, and leaves Phase 4 without a stable correlation record. |
| Using Twilio Content API now | Plain-text body from disk | Content API adds template management and variable handling that v1 explicitly defers. |

## Architecture Patterns

### Pattern 1: Draft Then Send
**What:** Create campaigns and materialize recipients first, then run a separate send step that consumes pending outbound rows.
**When to use:** Always for campaign execution in this phase.
**Why:** Prevents duplicate sends after partial failures and supports later retry/resume and callback correlation work.

### Pattern 2: Campaign Snapshot Storage
**What:** Store the loaded template file path and the resolved plain-text body on the campaign record.
**When to use:** At campaign creation time.
**Why:** The operator chose a file-driven workflow, but the app still needs an immutable local record of what was actually sent.

### Pattern 3: Canonical-Phone Recipient Materialization
**What:** Build recipients from eligible `PhoneNumberRecord` rows only, with a uniqueness constraint on `(CampaignId, PhoneNumberRecordId)`.
**When to use:** When drafting a campaign.
**Why:** This directly enforces `CAMP-02` and `CAMP-06`.

### Pattern 4: Provider Adapter for Sending
**What:** Add a Twilio messaging adapter that returns an internal send result contract with SID, initial status, and provider error details.
**When to use:** For all Twilio send calls.
**Why:** Keeps Twilio SDK types out of app/domain code and mirrors the Phase 2 adapter pattern.

## Twilio Messaging Findings

- `CreateMessageOptions` supports `MessagingServiceSid`, `Body`, and `StatusCallback`, and the helper library exposes `MessageResource.CreateAsync(...)` for sends.
- When `messaging_service_sid` is provided and `from` is omitted, Twilio selects the sender from the Messaging Service sender pool.
- If a request includes both `messaging_service_sid` and `status_callback`, the request-level callback URL overrides the Messaging Service callback URL.
- Twilio status tracking guidance is callback-first. That reinforces the choice to store the initial synchronous response in Phase 3 and leave callback ingestion for Phase 4 rather than adding polling loops now.
- Twilio’s initial message status may be `accepted`, `queued`, or another provider-returned state depending on how Twilio handles the send. Phase 3 should persist the returned initial status rather than assuming a single value.

## Recommended Data Shape

The most phase-aligned schema is:

- `Campaign`
  - local identity
  - name or operator label
  - template file path
  - template body snapshot
  - configured batch size
  - created / started / completed timestamps
  - campaign workflow status
- `CampaignRecipient` or `OutboundMessage`
  - local identity
  - `CampaignId`
  - `PhoneNumberRecordId`
  - current local send status
  - Twilio Message SID
  - initial provider status
  - provider error code / message
  - attempt timestamps

The uniqueness constraint must be on `(CampaignId, PhoneNumberRecordId)` so the same canonical phone can only appear once per campaign even if multiple `CUST_SID` links exist.

## Common Pitfalls

- **Using `CustomerPhoneLink` rows as recipients:** that would reintroduce duplicate sends for the same canonical phone number.
- **Reading the template file only at send time:** the file can change or disappear, making audits and retries inconsistent.
- **Sending without a local pending ledger row first:** a crash between Twilio acceptance and local persistence creates duplicate-send ambiguity.
- **Assuming all initial Twilio statuses are `accepted`:** persist the actual returned status string/value instead of normalizing too early.
- **Adding callback infrastructure to Phase 3:** callback ingress, authenticity validation, and idempotent status history belong to Phase 4.

## Code Shape Recommendations

### Message send request shape
```csharp
var options = new CreateMessageOptions(new PhoneNumber(to))
{
    MessagingServiceSid = messagingServiceSid,
    Body = body
};

var message = await MessageResource.CreateAsync(options, client);
```

### Recipient uniqueness rule
```csharp
builder.HasIndex(recipient => new { recipient.CampaignId, recipient.PhoneNumberRecordId })
    .IsUnique();
```

### Batch send selection
```csharp
var batch = await dbContext.CampaignRecipients
    .Where(recipient => recipient.CampaignId == campaignId && recipient.Status == Pending)
    .OrderBy(recipient => recipient.Id)
    .Take(batchSize)
    .ToListAsync(cancellationToken);
```

## Validation Architecture

- Continue using `xUnit` in `tests/PenelopeSMS.Tests/PenelopeSMS.Tests.csproj`.
- Use SQLite-backed tests for campaign schema behavior, recipient materialization, and send workflow persistence.
- Fake the Twilio messaging adapter for all routine tests so batching and provider-response handling stay deterministic.
- Keep quick feedback with `dotnet test PenelopeSMS.sln --no-restore` after each task and a full suite run after each plan.
- Add one manual live-provider verification step before phase sign-off: send a small campaign against a safe test number with real Twilio credentials and confirm the stored Message SID plus initial status in SQL Server.

## Sources

- Twilio Message Resource: https://www.twilio.com/docs/messaging/api/message-resource
- Twilio outbound message status guide: https://www.twilio.com/docs/messaging/guides/track-outbound-message-status
- Twilio Messaging Services overview: https://www.twilio.com/docs/messaging/services
- Twilio .NET helper library repository: https://github.com/twilio/twilio-csharp

---
*Phase research completed: 2026-03-12*
