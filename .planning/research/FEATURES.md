# Feature Research

**Domain:** Single-operator SMS campaign operations app
**Researched:** 2026-03-12
**Confidence:** HIGH

## Feature Landscape

### Table Stakes (Users Expect These)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Oracle import with source identifiers | The operator needs a repeatable way to load the working audience from the system of record | MEDIUM | Must capture phone number plus `CUST_SID` and preserve import history. |
| Global deduplication with source linkage | Marketing tools must avoid duplicate sends while preserving who each number belongs to | MEDIUM | Separate number identity from customer-number relationships. |
| Number normalization and line-type enrichment | Campaign eligibility depends on filtering out obvious non-mobile/non-SMS candidates | MEDIUM | Twilio Lookup supports carrier and line type; exact SMS reachability is not a guaranteed low-cost field today. |
| Batch campaign send | Sending one message at a time manually is not operationally useful | MEDIUM | Batch size should be configurable and restart-safe. |
| Delivery status persistence | A campaign tool is incomplete without auditable message outcomes | MEDIUM | Store Twilio SID, initial status, callback statuses, and error details. |
| Failure visibility | The operator needs to know what failed and why | LOW | Error codes and last status should be queryable in the console. |

### Differentiators (Competitive Advantage)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| AWS SQS-backed callback ingestion | Lets the local app consume delivery updates asynchronously without polling Twilio for every SID | HIGH | Requires a public callback bridge plus local queue consumer. |
| Eligibility preview before send | Prevents wasting spend on unverified or recently failed numbers | LOW | Show counts for total imported, deduped, verified mobile, and sendable. |
| Restart-safe campaign ledger | Protects against duplicate sends after crashes or operator restarts | MEDIUM | Store per-recipient send state before and after API submission. |
| Reconciliation mode | Catches missed callbacks and stale statuses | MEDIUM | Poll Twilio only for messages still unresolved after a threshold. |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Personalized merge fields in v1 | Seems useful for marketing | Expands template, data-quality, and preview complexity without helping core pipeline validation | Plain-text static template for v1 |
| Rich dashboard UI | Feels more polished | Adds UI work before the operator workflow is proven | Console summaries plus SQL-backed reports |
| Real-time polling of every message SID | Feels simpler than webhooks | Expensive, noisy, and operationally fragile at campaign scale | Status callbacks with reconciliation polling only for gaps |
| Multi-user auth and roles | Common enterprise reflex | No value for a single-user local operator app | Omit until the operator model changes |

## Feature Dependencies

```text
Oracle import
    └──requires──> SQL Server schema

Deduplication
    └──requires──> imported raw numbers

Campaign send
    └──requires──> verified/eligible numbers
                       └──requires──> Twilio Lookup enrichment

Delivery monitoring
    └──requires──> outbound message ledger
                       └──requires──> Twilio SID persistence

SQS callback processing
    └──requires──> public callback bridge
```

### Dependency Notes

- **Import requires SQL schema:** raw source rows, canonical numbers, and cross-reference tables need to exist before Oracle data can be loaded safely.
- **Campaign send requires verification:** messaging should target only numbers that pass the chosen eligibility rule set.
- **Delivery monitoring requires message ledger:** callbacks are only useful if every outbound attempt is stored with a correlation key before send completion.
- **SQS callback processing requires a public callback bridge:** Twilio sends webhooks to public HTTP endpoints, not directly to a local SQL Server or local console app.

## MVP Definition

### Launch With (v1)

- [ ] Oracle import of phone numbers and `CUST_SID` into the local SQL Server database — core entry point for all later steps
- [ ] Canonical-number deduplication and customer-number relationship tracking — prevents duplicate sends
- [ ] Twilio Lookup enrichment of imported numbers — supports carrier/line-type based eligibility
- [ ] Plain-text campaign send in configurable batches — core operator value
- [ ] Delivery status ingestion back into the database — closes the loop operationally

### Add After Validation (v1.x)

- [ ] Campaign dry-run / preview counts — add when the send workflow is stable
- [ ] Retry policies and resume controls — add after real campaign failures are observed
- [ ] Reconciliation job for unresolved statuses — add when callback gaps need operational hardening

### Future Consideration (v2+)

- [ ] Template variables / personalization — defer until the static-template workflow is proven
- [ ] Segmentation rules beyond the Oracle source query — defer until campaign strategy broadens
- [ ] Multi-channel messaging or MMS — not essential to the core SMS pipeline

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Oracle import | HIGH | MEDIUM | P1 |
| Global deduplication | HIGH | MEDIUM | P1 |
| Twilio Lookup enrichment | HIGH | MEDIUM | P1 |
| Batch SMS sending | HIGH | MEDIUM | P1 |
| Delivery status persistence | HIGH | MEDIUM | P1 |
| SQS-backed callback ingestion | HIGH | HIGH | P1 |
| Reconciliation polling | MEDIUM | MEDIUM | P2 |
| Dry-run preview | MEDIUM | LOW | P2 |
| Personalized templates | LOW | MEDIUM | P3 |

**Priority key:**
- P1: Must have for launch
- P2: Should have, add when possible
- P3: Nice to have, future consideration

## Competitor Feature Analysis

| Feature | Competitor A | Competitor B | Our Approach |
|---------|--------------|--------------|--------------|
| Audience import | Bulk upload / connector | Bulk upload / sync | Oracle import purpose-built for one source system |
| Number hygiene | Built-in number intelligence | External enrichment add-on | Twilio Lookup-based enrichment before sends |
| Delivery tracking | Hosted dashboards | Hosted dashboards | Local database plus callback-driven status updates |
| Campaign builder | Rich UI | Rich UI | Minimal operator-focused console workflow |

## Sources

- Twilio Lookup docs: https://www.twilio.com/docs/lookup/v2-api
- Twilio Messaging status callback docs: https://www.twilio.com/docs/messaging/guides/track-outbound-message-status
- Twilio delivery logging best practices: https://www.twilio.com/docs/messaging/guides/outbound-message-logging
- Twilio A2P 10DLC docs: https://www.twilio.com/docs/messaging/compliance/a2p-10dlc
- AWS SQS receive/long polling docs: https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/ReceiveMessage.html and https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/best-practices-setting-up-long-polling.html

---
*Feature research for: SMS campaign operations app*
*Researched: 2026-03-12*
