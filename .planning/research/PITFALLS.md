# Pitfalls Research

**Domain:** Twilio-backed SMS campaign operations app
**Researched:** 2026-03-12
**Confidence:** HIGH

## Critical Pitfalls

### Pitfall 1: Using Twilio Verify for list enrichment

**What goes wrong:**
The implementation is aimed at the wrong API family and never cleanly produces the carrier/line-type data the app actually needs.

**Why it happens:**
The words "verify" and "valid phone number" sound close enough that teams conflate OTP verification with line intelligence.

**How to avoid:**
Use Twilio Lookup with `line_type_intelligence` for enrichment. Keep Twilio Verify out of scope unless the product later adds OTP flows.

**Warning signs:**
Designs or code talk about Verify Services, OTPs, or verification codes for an offline marketing list.

**Phase to address:**
Phase 2

---

### Pitfall 2: Treating line type as a perfect SMS-capable truth source

**What goes wrong:**
The app marks numbers as definitively SMS-capable when the chosen low-cost data source only reports line type and carrier, not guaranteed deliverability.

**Why it happens:**
Teams over-read the meaning of "mobile" or "non-fixed VoIP" and skip delivery-feedback validation.

**How to avoid:**
Model eligibility as a rule based on Lookup data plus observed messaging outcomes. Keep a distinct field for "Lookup classification" versus "delivery evidence".

**Warning signs:**
Requirements or schema collapse `line_type`, `carrier`, and `sms_capable` into one field without provenance.

**Phase to address:**
Phase 2 and Phase 4

---

### Pitfall 3: Skipping A2P 10DLC / Messaging Service setup

**What goes wrong:**
US marketing traffic is filtered, blocked, delayed, or launched late because sender setup was treated as an afterthought.

**Why it happens:**
The app is local and simple, so teams assume carrier registration is unrelated to application planning.

**How to avoid:**
Treat Twilio sender provisioning and campaign registration as a dependency, not an ops footnote. Use a Messaging Service and register the relevant use case before real sends.

**Warning signs:**
No sender strategy is documented, or the design assumes arbitrary Twilio numbers can immediately send US marketing traffic.

**Phase to address:**
Phase 3

---

### Pitfall 4: Building callback ingestion without authenticity or idempotency

**What goes wrong:**
Fraudulent or duplicate callbacks mutate message state incorrectly, producing unreliable campaign reporting.

**Why it happens:**
Webhook handlers are often treated as "just accept the POST".

**How to avoid:**
Validate Twilio signatures, keep the exact callback URL stable for validation, and process callbacks idempotently using Message SID plus status history rules.

**Warning signs:**
The callback handler ignores `X-Twilio-Signature`, overwrites status blindly, or cannot handle repeated events.

**Phase to address:**
Phase 4

---

### Pitfall 5: Polling every Message SID instead of using callbacks

**What goes wrong:**
Monitoring becomes slow, expensive, and operationally noisy, especially during large campaigns.

**Why it happens:**
Polling feels simpler than standing up callback infrastructure.

**How to avoid:**
Use status callbacks as the primary channel and polling only for reconciliation of unresolved records after a time threshold.

**Warning signs:**
The design centers on loops over all sent SIDs with no callback path.

**Phase to address:**
Phase 4

---

### Pitfall 6: Deduplicating away customer traceability

**What goes wrong:**
The app prevents duplicate sends but loses which `CUST_SID` values were tied to a canonical number.

**Why it happens:**
Teams model one table of unique phone numbers and discard source relationships during import.

**How to avoid:**
Use separate entities for canonical phone numbers and customer-phone associations, with duplicate flags and import lineage.

**Warning signs:**
There is no join table or relationship history between numbers and Oracle customer identifiers.

**Phase to address:**
Phase 1

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Hard-coding batch sizes and endpoints | Fast prototype | Rebuild required for every environment change | Only in a throwaway spike |
| Storing only current delivery status | Simpler schema | No audit trail or callback replay diagnostics | Never for campaign history |
| Sending directly from import query output | Fewer tables initially | No dedupe, no resumability, no verification ledger | Never |
| Using ad hoc SQL scripts for schema changes | Feels fast early | Drift, missing repeatability, hard recovery | Only before the first shared install; migrate immediately after |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Twilio Lookup | Assuming Lookup Basic returns carrier/line type | Request `line_type_intelligence` explicitly |
| Twilio Messaging callbacks | Pointing `StatusCallback` at a non-public or unstable URL | Use a stable public HTTPS endpoint |
| Twilio webhook security | IP allow-listing only | Validate signatures; Twilio does not publish fixed callback IP ranges |
| AWS SQS | Short polling with low visibility timeout | Use long polling and align visibility timeout to processing duration |
| Oracle import | Pulling entire source rows repeatedly | Query only required fields and chunk the import |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Row-by-row upserts during import | Long imports and lock contention | Batch writes and use indexed canonical-number lookups | Tens of thousands of rows |
| Serial Twilio Lookup calls | Verification takes hours | Use controlled concurrency and checkpoint progress | Large imports around 70k numbers |
| Per-message synchronous send logging without batching | Slow campaign execution | Prestage recipients, send in configurable batches, and commit progress in chunks | Medium to large campaigns |
| Aggressive empty SQS polling | Excess API calls and noisy logs | Use long polling and batch receives | Always, but especially on idle queues |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Storing Twilio and AWS secrets in source | Credential compromise | Use config files outside source, environment variables, or a secret store |
| Accepting webhook payloads without signature checks | Fraudulent status updates | Validate `X-Twilio-Signature` using Twilio SDK helpers |
| Logging full sensitive payloads blindly | Data leakage in local logs | Log only necessary identifiers and error context |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Menu items that hide destructive scope | Operator accidentally reimports or resends | Show counts, filters, and confirmation summaries before write-heavy actions |
| No progress checkpointing | Operator cannot tell whether a large job is safe to stop | Persist job progress and show resumable counts |
| Status screens with no totals | Hard to know if the campaign is healthy | Always show totals by state and last error counts |

## "Looks Done But Isn't" Checklist

- [ ] **Import:** Often missing duplicate relationship tracking — verify one phone can link to multiple `CUST_SID` values
- [ ] **Verification:** Often missing provenance fields — verify stored data distinguishes Twilio Lookup output from derived eligibility
- [ ] **Campaign send:** Often missing persisted Twilio SID before callback arrival — verify every accepted send is recorded immediately
- [ ] **Monitoring:** Often missing callback validation and replay handling — verify duplicate callbacks do not corrupt status history
- [ ] **Compliance:** Often missing sender registration readiness — verify Twilio sender / Messaging Service setup is documented before launch

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Wrong Twilio API chosen | MEDIUM | Swap to Lookup, migrate schema fields, rerun enrichment |
| Missed callbacks | MEDIUM | Poll unresolved message SIDs, backfill statuses, inspect callback logs |
| Duplicate sends after crash | HIGH | Reconcile campaign ledger, mark accepted SIDs, suppress resend for already-submitted recipients |
| Bad dedupe model | HIGH | Rebuild canonical-number and association tables from import history |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Dedupe losing `CUST_SID` traceability | Phase 1 | Import the same phone for multiple customers and confirm both associations remain |
| Wrong Twilio enrichment API | Phase 2 | Enrichment stores line type and carrier from Lookup, not Verify artifacts |
| Missing sender/compliance setup | Phase 3 | Sender strategy and Messaging Service prerequisites are documented before test sends |
| Callback insecurity / non-idempotency | Phase 4 | Replay the same callback twice and confirm no duplicate side effects |
| Polling-first monitoring | Phase 4 | Delivery states arrive through callback ingestion, with polling reserved for reconciliation |

## Sources

- Twilio Lookup docs: https://www.twilio.com/docs/lookup/v2-api
- Twilio Verify docs: https://www.twilio.com/docs/verify/api
- Twilio status callback docs: https://www.twilio.com/docs/messaging/guides/track-outbound-message-status
- Twilio delivery logging guidance: https://www.twilio.com/docs/messaging/guides/outbound-message-logging
- Twilio webhook security: https://www.twilio.com/docs/usage/webhooks/webhooks-security
- Twilio A2P 10DLC docs: https://www.twilio.com/docs/messaging/compliance/a2p-10dlc
- AWS SQS polling guidance: https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/best-practices-setting-up-long-polling.html

---
*Pitfalls research for: PenelopeSMS*
*Researched: 2026-03-12*
