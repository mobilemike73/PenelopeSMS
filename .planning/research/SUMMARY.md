# Project Research Summary

**Project:** PenelopeSMS
**Domain:** Local SMS campaign operations application
**Researched:** 2026-03-12
**Confidence:** HIGH

## Executive Summary

PenelopeSMS fits a well-understood pattern: keep operator workflows and operational state in a local .NET application, but treat Twilio callbacks as an internet-facing integration that should flow through a stable public ingress path before the local app processes them. For this project, the cleanest fit is a .NET 9 Generic Host console app with SQL Server Express as the system of record, ODP.NET Core for Oracle import, Twilio Lookup for number enrichment, Twilio Messaging for sends, and AWS SQS as the asynchronous callback handoff back to the local operator machine.

The main technical correction from research is that Twilio Verify is not the right product for classifying imported phone numbers. Twilio Lookup with `line_type_intelligence` is the low-cost official option for carrier and line-type intelligence. That said, Lookup line type is not the same thing as guaranteed SMS reachability, so the roadmap should keep delivery evidence and reconciliation in scope instead of treating Lookup as perfect truth.

The main launch risk is callback integrity. Twilio sender provisioning is already complete for this project, so the remaining operational concern is making callback ingestion secure, durable, and idempotent.

## Key Findings

### Recommended Stack

The recommended implementation is a hosted console monolith on .NET 9 using Generic Host patterns, EF Core 9 for the local SQL Server Express database, and ODP.NET Core for Oracle reads. Twilio should be integrated through its official .NET SDK, with Lookup used for enrichment and Programmable Messaging used for campaign execution. AWS SQS is the right fit for queue-based callback smoothing when the main app remains local.

**Core technologies:**
- .NET 9: host, DI, configuration, logging, and background services for a console app
- EF Core 9 + SQL Server provider: operational data model and migrations for local SQL Server Express
- ODP.NET Core: read-only Oracle import access without dual-ORM complexity
- Twilio Lookup / Messaging: number enrichment and outbound message execution
- AWS SQS: asynchronous callback buffer for delivery result ingestion

### Expected Features

Core expectations in this domain are straightforward import, deduplication, enrichment, batch sending, and durable delivery-state tracking. The most valuable non-table-stakes feature for this project is asynchronous callback ingestion so the local app does not need to poll Twilio for every message SID.

**Must have (table stakes):**
- Oracle import with `CUST_SID` preservation
- Global deduplication with source relationship tracking
- Twilio-based number enrichment
- Configurable batch campaign sending
- Delivery status persistence and failure visibility

**Should have (competitive):**
- SQS-backed callback processing
- Eligibility preview before send
- Restart-safe campaign ledger

**Defer (v2+):**
- Template personalization
- Rich UI
- Multi-user or role-based controls

### Architecture Approach

The architecture should separate local workflow orchestration from public callback ingress. The console app owns menu actions, database writes, import logic, verification logic, campaign orchestration, and local queue consumption. A separate public ingress path receives Twilio callbacks and forwards them into SQS, which the local app long-polls and applies idempotently to message records.

**Major components:**
1. Console host — menu flows, orchestration, and job progress reporting
2. Local SQL Server schema — numbers, imports, campaigns, messages, and status history
3. Oracle import adapter — read-only source extraction and normalization
4. Twilio integration layer — Lookup enrichment and outbound Messaging sends
5. Callback bridge + SQS consumer — secure, asynchronous delivery result ingestion

### Critical Pitfalls

1. **Using Verify instead of Lookup** — use Twilio Lookup for line intelligence and keep Verify out of this MVP
2. **Treating Lookup as perfect SMS-capable truth** — store provider facts separately from derived eligibility and delivery evidence
3. **Skipping webhook validation and idempotency** — validate signatures and tolerate duplicate callbacks
4. **Polling every SID** — use callbacks first, then reconcile unresolved statuses later

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Data Foundation
**Rationale:** Everything depends on a correct local schema, canonical-number model, and Oracle import path.
**Delivers:** SQL Server schema, import workflow, dedupe model, and import auditability.
**Addresses:** Oracle import and dedupe features.
**Avoids:** Losing `CUST_SID` traceability during deduplication.

### Phase 2: Number Intelligence
**Rationale:** Campaign eligibility depends on enrichment output before any real messaging work starts.
**Delivers:** Twilio Lookup integration, stored carrier/line-type metadata, and eligibility rules.
**Uses:** Twilio Lookup and throttled background processing.
**Implements:** Verification / enrichment component.

### Phase 3: Campaign Execution
**Rationale:** Once the audience model is trustworthy, the app can create campaigns and send batches safely.
**Delivers:** Template-file loading, campaign ledger, batch sends, and Twilio SID persistence.
**Uses:** Twilio Messaging and SQL-backed message state.
**Implements:** Campaign orchestration component.

### Phase 4: Delivery Feedback
**Rationale:** Campaign execution is incomplete without asynchronous status updates and callback security.
**Delivers:** Callback ingress architecture, SQS consumer, idempotent status updates, and monitoring summaries.
**Uses:** Public callback bridge plus SQS long polling.
**Implements:** Delivery-monitoring component.

### Phase 5: Hardening and Reconciliation
**Rationale:** After the end-to-end loop exists, add recoverability, reconciliation, and operational polish.
**Delivers:** Retry/reconcile flows, unresolved-status polling, better summaries, and production-readiness checks.

### Phase Ordering Rationale

- Import and dedupe must precede enrichment because verification works on the canonical-number set.
- Enrichment must precede campaign execution because sends should target only eligible numbers.
- Campaign ledgering must exist before callback processing so delivery events have durable correlation targets.
- Callback ingestion comes after send execution but before declaring v1 complete because the user's definition of done includes delivery-result updates.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 2:** precise eligibility rules, Twilio Lookup throughput/cost handling, and result-field mapping
- **Phase 4:** final callback bridge shape, signature validation, and SQS operational settings
- **Phase 5:** reconciliation thresholds and recovery policies

Phases with standard patterns (skip research-phase):
- **Phase 1:** local schema, import workflow, and dedupe modeling are standard and well-understood

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Core technologies verified from official Microsoft, Oracle, Twilio, and AWS docs |
| Features | HIGH | User intent and vendor docs align cleanly |
| Architecture | HIGH | Public-ingress/private-processing split is strongly supported by Twilio callback behavior |
| Pitfalls | HIGH | Major risks are explicit in official docs or directly inferred from those constraints |

**Overall confidence:** HIGH

### Gaps to Address

- **Exact SMS-capable rule:** lowest-cost Twilio Lookup data gives line type and carrier, not a perfect SMS-capable guarantee; finalize the app's eligibility rule during planning.
- **Callback bridge deployment choice:** decide between AWS-native ingress only versus a small self-hosted ASP.NET Core bridge.

## Sources

### Primary (HIGH confidence)
- https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host — console host architecture
- https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-9.0/whatsnew — EF Core 9 lifecycle
- https://learn.microsoft.com/en-us/ef/core/providers/sql-server/ — SQL Server provider guidance
- https://www.twilio.com/docs/lookup/v2-api — Lookup capabilities
- https://www.twilio.com/docs/lookup/v2-api/line-type-intelligence — line-type/carrier details
- https://www.twilio.com/docs/messaging/guides/track-outbound-message-status — status callback model
- https://www.twilio.com/docs/messaging/guides/outbound-message-logging — callback-first logging guidance
- https://www.twilio.com/docs/usage/webhooks/webhooks-security — webhook validation guidance
- https://www.twilio.com/docs/messaging/compliance/a2p-10dlc — US sender compliance guidance
- https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/ReceiveMessage.html — SQS receive pattern
- https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/best-practices-setting-up-long-polling.html — SQS long-poll guidance

### Secondary (MEDIUM confidence)
- https://www.twilio.com/en-us/blog/serverless-twilio-status-callback-aws — official example of AWS-based callback smoothing and downstream processing
- https://docs.oracle.com/en/database/oracle/oracle-database/26/odpnt/EFCore9features.html — Oracle EF Core support notes used to justify keeping Oracle read-only and thin

---
*Research completed: 2026-03-12*
*Ready for roadmap: yes*
