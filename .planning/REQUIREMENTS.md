# Requirements: PenelopeSMS

**Defined:** 2026-03-12
**Core Value:** The operator can reliably import, verify, and message eligible phone numbers while keeping campaign state and delivery outcomes accurate in the app database.

## v1 Requirements

Requirements for initial release. Each maps to roadmap phases.

### Import

- [x] **IMPT-01**: Operator can import Oracle source rows containing phone number and `CUST_SID` into the local SQL Server database.
- [x] **IMPT-02**: App normalizes imported phone numbers into a canonical format before deduplication and storage.
- [x] **IMPT-03**: App deduplicates phone numbers globally while preserving all `CUST_SID` associations to the canonical phone record.
- [x] **IMPT-04**: App records import batch metadata, including started time, completed time, rows read, rows imported, and rows rejected.

### Enrichment

- [x] **ENRH-01**: Operator can run Twilio number enrichment for imported phone records that have not yet been enriched or are due for refresh.
- [x] **ENRH-02**: App stores Twilio line type, carrier, country code, last enriched timestamp, and raw provider payload or mapped equivalent for each enriched phone record.
- [x] **ENRH-03**: App derives a campaign-eligibility flag from stored enrichment data without overwriting the original provider facts.
- [x] **ENRH-04**: App records enrichment failures, error details, and retry status per phone record.

### Campaigns

- [x] **CAMP-01**: Operator can create a campaign from a plain-text template file stored on disk.
- [x] **CAMP-02**: App creates a campaign recipient list from eligible phone records only.
- [x] **CAMP-03**: Operator can configure the batch size used for outbound sends.
- [x] **CAMP-04**: App sends campaign messages through Twilio in batches and persists a message ledger row for every attempted recipient.
- [x] **CAMP-05**: App stores the Twilio Message SID and initial send status for every accepted outbound message.
- [x] **CAMP-06**: App prevents duplicate sends to the same canonical phone number within the same campaign.

### Monitoring

- [x] **MON-01**: App can ingest Twilio delivery-status callbacks through the configured callback pipeline without querying every Message SID individually.
- [x] **MON-02**: App can consume Twilio delivery events from AWS SQS and apply them idempotently to the matching outbound message record.
- [x] **MON-03**: App stores delivery status history, including status transition time, Twilio error code, and provider message when available.
- [ ] **MON-04**: Operator can view campaign progress totals for pending, submitted, sent, delivered, undelivered, and failed recipients.

### Operations

- [x] **OPER-01**: Operator can configure Oracle, SQL Server, Twilio, and AWS settings without modifying source code.
- [ ] **OPER-02**: App writes console-visible progress and error information for import, enrichment, campaign send, and callback processing jobs.

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Campaigns

- **CAMP-07**: Operator can preview recipient counts and estimated eligibility before launching a campaign.
- **CAMP-08**: Operator can resume or retry failed campaign batches from the console.
- **CAMP-09**: Operator can reconcile unresolved message statuses by polling Twilio selectively after a configurable delay.

### Templates

- **TMPL-01**: Operator can use template variables populated from source or local data fields.

### Administration

- **ADMIN-01**: Operator can manage multiple Twilio sender profiles from the console.
- **ADMIN-02**: App supports multiple local operators with authentication and audit attribution.

## Out of Scope

| Feature | Reason |
|---------|--------|
| GUI / desktop windowed interface | Console menus are sufficient for the single-operator v1 workflow |
| Personalized message templates | Static plain-text templates reduce complexity and are enough to validate the core pipeline |
| Per-message polling as the primary monitoring model | Twilio callbacks plus SQS are the desired monitoring path |
| Cross-platform support | The app is explicitly Windows-only |
| Multi-user authentication and authorization | The app is for one internal operator |
| Twilio sender provisioning / 10DLC registration | Twilio account setup is already complete outside the app |

## Traceability

To be populated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| IMPT-01 | Phase 1 | Complete |
| IMPT-02 | Phase 1 | Complete |
| IMPT-03 | Phase 1 | Complete |
| IMPT-04 | Phase 1 | Complete |
| ENRH-01 | Phase 2 | Complete |
| ENRH-02 | Phase 2 | Complete |
| ENRH-03 | Phase 2 | Complete |
| ENRH-04 | Phase 2 | Complete |
| CAMP-01 | Phase 3 | Complete |
| CAMP-02 | Phase 3 | Complete |
| CAMP-03 | Phase 3 | Complete |
| CAMP-04 | Phase 3 | Complete |
| CAMP-05 | Phase 3 | Complete |
| CAMP-06 | Phase 3 | Complete |
| MON-01 | Phase 4 | Complete |
| MON-02 | Phase 4 | Complete |
| MON-03 | Phase 4 | Complete |
| MON-04 | Phase 5 | Pending |
| OPER-01 | Phase 1 | Complete |
| OPER-02 | Phase 5 | Pending |

**Coverage:**
- v1 requirements: 20 total
- Mapped to phases: 20
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-12*
*Last updated: 2026-03-12 after Phase 4 completion*
