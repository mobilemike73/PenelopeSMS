# Roadmap: PenelopeSMS

## Overview

PenelopeSMS will move from local data foundation to end-to-end SMS campaign execution in five phases: first establish the SQL Server schema and Oracle import flow, then enrich and classify numbers with Twilio, then execute campaigns in controlled batches, then ingest asynchronous delivery updates, and finally add the operator-facing monitoring and hardening needed to run the workflow reliably.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Data Foundation** - Create the local data model, configuration, and Oracle import pipeline.
- [x] **Phase 2: Number Intelligence** - Enrich imported numbers with Twilio and derive campaign eligibility. (completed 2026-03-12)
- [ ] **Phase 3: Campaign Execution** - Create campaigns from template files and send SMS in configurable batches.
- [ ] **Phase 4: Delivery Pipeline** - Ingest Twilio delivery callbacks through the callback pipeline and update message history.
- [ ] **Phase 5: Operator Monitoring** - Surface campaign progress, job visibility, and operational hardening in the console app.

## Phase Details

### Phase 1: Data Foundation
**Goal**: Deliver the local SQL Server schema, environment configuration, and Oracle import workflow that produce canonical phone records and preserved `CUST_SID` relationships.
**Depends on**: Nothing (first phase)
**Requirements**: [IMPT-01, IMPT-02, IMPT-03, IMPT-04, OPER-01]
**Success Criteria** (what must be TRUE):
  1. Operator can configure Oracle and SQL Server connection settings without changing source code.
  2. Operator can run an import that loads Oracle phone rows and `CUST_SID` values into SQL Server.
  3. Imported phone data is normalized into a canonical format and duplicate numbers are linked to all relevant `CUST_SID` values.
  4. Import history shows batch-level totals for read, imported, and rejected rows.
**Plans**: 3 plans

Plans:
- [x] 01-01: Bootstrap the solution, host, configuration model, and test infrastructure
- [x] 01-02: Build the canonical SQL Server data model, normalization rules, and import-audit persistence
- [x] 01-03: Wire the Oracle import adapter and console workflow onto the Phase 1 foundation

### Phase 2: Number Intelligence
**Goal**: Deliver Twilio-based enrichment that stores provider facts and derived eligibility for imported phone records.
**Depends on**: Phase 1
**Requirements**: [ENRH-01, ENRH-02, ENRH-03, ENRH-04]
**Success Criteria** (what must be TRUE):
  1. Operator can run enrichment for imported phone numbers that need classification.
  2. Each enriched phone record stores line type, carrier, enrichment timestamp, and provider payload or equivalent mapped data.
  3. Eligibility for campaigns is derived from stored enrichment data without losing the original provider facts.
  4. Failed enrichment attempts are visible with retry-relevant error details.
**Plans**: 3 plans

Plans:
- [x] 02-01: Add the enrichment snapshot schema and Twilio Lookup adapter foundation
- [x] 02-02: Build the default/full-refresh enrichment workflow and due-record targeting
- [x] 02-03: Add failed-record review plus retry-all and retry-selected actions

### Phase 3: Campaign Execution
**Goal**: Deliver campaign creation and batched outbound messaging using eligible phone records only.
**Depends on**: Phase 2
**Requirements**: [CAMP-01, CAMP-02, CAMP-03, CAMP-04, CAMP-05, CAMP-06]
**Success Criteria** (what must be TRUE):
  1. Operator can create a campaign from a plain-text template file.
  2. Campaign recipient selection excludes ineligible numbers and duplicate sends within the same campaign.
  3. Operator can choose a batch size and send the campaign in controlled batches.
  4. Every accepted outbound message stores a Twilio Message SID and initial send status in the local database.
**Plans**: 3 plans

Plans:
- [ ] 03-01: Add the campaign ledger schema plus template and Twilio send foundations
- [ ] 03-02: Build campaign drafting from plain-text templates and eligible recipient materialization
- [ ] 03-03: Send drafted campaigns in configured batches and persist initial Twilio results

### Phase 4: Delivery Pipeline
**Goal**: Deliver asynchronous delivery-result ingestion from Twilio callbacks through AWS SQS into the local database.
**Depends on**: Phase 3
**Requirements**: [MON-01, MON-02, MON-03]
**Success Criteria** (what must be TRUE):
  1. Twilio delivery events reach the app through the configured callback pipeline without per-message polling.
  2. The app can consume delivery events from AWS SQS and apply them idempotently to message records.
  3. Message history stores delivery-status transitions, timestamps, and provider error details when present.
**Plans**: TBD

### Phase 5: Operator Monitoring
**Goal**: Deliver operator-facing visibility and operational logging for import, enrichment, sending, and delivery processing.
**Depends on**: Phase 4
**Requirements**: [MON-04, OPER-02]
**Success Criteria** (what must be TRUE):
  1. Operator can view campaign totals by pending, submitted, sent, delivered, undelivered, and failed states.
  2. Import, enrichment, campaign send, and callback-processing jobs emit useful console-visible progress and error information.
  3. The app provides enough local visibility to understand current campaign state without querying Twilio message records one by one.
**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3 -> 4 -> 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Data Foundation | 3/3 | Complete | 2026-03-12 |
| 2. Number Intelligence | 3/3 | Complete | 2026-03-12 |
| 3. Campaign Execution | 0/3 | Planned | - |
| 4. Delivery Pipeline | 0/TBD | Not started | - |
| 5. Operator Monitoring | 0/TBD | Not started | - |
