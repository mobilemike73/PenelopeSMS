# PenelopeSMS

## What This Is

PenelopeSMS is a Windows-only .NET 9 console application for a single internal operator to import customer phone numbers from Oracle into a local SQL Server Express database, verify and enrich those numbers with Twilio, and run SMS marketing campaigns in controlled batches. The app persists import, verification, campaign, and delivery-result data so the operator can work from a basic console menu instead of ad hoc scripts.

## Core Value

The operator can reliably import, verify, and message eligible phone numbers while keeping campaign state and delivery outcomes accurate in the app database.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Import phone numbers and `CUST_SID` values from Oracle into the app database.
- [ ] Deduplicate phone numbers globally while preserving their relationships to one or more `CUST_SID` values.
- [ ] Verify and enrich imported phone numbers using Twilio so only SMS-capable mobile numbers are eligible for campaigns.
- [ ] Execute plain-text SMS campaigns in configurable batch sizes from a text template file.
- [ ] Capture asynchronous delivery outcomes and update campaign/message records automatically.

### Out of Scope

- Multi-user access or authentication — the app is for a single internal operator only.
- Graphical UI beyond a basic console menu — a console workflow is sufficient for v1.
- Personalized or variable-substituted message templates — v1 sends plain-text content only.
- Cross-platform support — the app is explicitly Windows-only.

## Context

The source system is an Oracle database that provides customer phone numbers and `CUST_SID`, a unique customer identifier. The destination system is local SQL Server Express on `.\SQLEXPRESS` / `127.0.0.1,1433`.

Expected data volume is roughly 70,000 phone numbers. Imports should support global deduplication of phone numbers while flagging duplicate relationships under all associated `CUST_SID` values.

All imported numbers are considered eligible from a business-rules perspective; Twilio-driven phone intelligence determines whether a number is mobile and SMS-capable. The operator wants to store carrier information and any other useful low-cost metadata returned by the Twilio query.

Campaigns send plain-text SMS messages loaded from a template file in configurable batch sizes. Delivery status should flow back into the database automatically rather than by polling every message SID individually.

## Constraints

- **Platform**: Windows only — no cross-platform requirement for v1.
- **Runtime**: .NET 9 console app — no web or desktop GUI planned.
- **Database**: SQL Server Express local instance at `.\SQLEXPRESS` / `127.0.0.1,1433` — app state must persist there.
- **Source Dependency**: Oracle database access is required for initial and repeat imports.
- **Operator Model**: Single-user local operation — no authentication, roles, or concurrent-user design needed.
- **Messaging**: SMS only, plain text only — no MMS or personalization in v1.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Build as a .NET 9 console application with menu-driven flows | Matches the desired low-overhead operator experience and avoids unnecessary UI work | — Pending |
| Use SQL Server Express as the system of record for imported numbers, verification data, campaigns, and delivery statuses | Centralizes operational state locally and supports repeatable workflows | — Pending |
| Deduplicate phone numbers globally while preserving links to all associated `CUST_SID` values | Prevents duplicate messaging while retaining source-system traceability | — Pending |
| Treat Twilio phone intelligence as an enrichment/eligibility step before campaign sends | Campaign targeting depends on excluding non-mobile or non-SMS-capable numbers | — Pending |
| Prefer asynchronous delivery updates over per-message polling | Reduces operational overhead and avoids querying every message SID individually | — Pending |
| Re-evaluate whether Twilio Lookup, rather than Verify, is the correct service for phone type and carrier intelligence | The stated goal is number classification/enrichment, which may not match Verify's primary purpose | — Pending |

---
*Last updated: 2026-03-12 after initialization*
