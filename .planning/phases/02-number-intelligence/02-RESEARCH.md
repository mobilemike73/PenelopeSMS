# Phase 2: Number Intelligence - Research

**Researched:** 2026-03-12
**Domain:** Twilio Lookup v2 enrichment, eligibility derivation, refresh targeting, and retryable failure handling for imported phone records
**Confidence:** HIGH

## User Constraints

Phase 2 planning is constrained by the approved context in `02-CONTEXT.md` and the completed Phase 1 foundation:

- Only `US mobile` numbers are campaign-eligible.
- Unknown, unclassified, or uncertain Twilio results are ineligible.
- The latest enrichment result determines eligibility.
- Default enrichment covers never-enriched records, failed records, and successful records that have become due for refresh after `30 days`.
- The operator needs a default enrichment mode plus a separate full-refresh option.
- Store mapped Twilio facts plus the full raw provider payload, but keep only the latest successful snapshot on the phone record in this phase.
- Show summary counts plus a failed-record list, with both retry-all and retry-selected actions for retryable failures.

## Summary

Phase 2 should build on the existing `PhoneNumberRecord` model rather than creating a separate historical enrichment ledger. Twilio Lookup v2 always returns basic validation fields such as `valid`, `validation_errors`, and ISO `country_code`, while the paid `line_type_intelligence` package adds `type`, `carrier_name`, `mobile_country_code`, `mobile_network_code`, and a package-level `error_code`. That directly matches the phase requirement to store line type, carrier, country code, enrichment timestamp, and raw provider payload.

The highest-leverage implementation is to keep Twilio behind a dedicated infrastructure adapter so the enrichment workflow can be tested entirely with fakes. Phase 1 already established the adapter pattern for Oracle and the scoped console workflow. Reusing that pattern avoids live-provider coupling in tests and keeps refresh targeting, eligibility derivation, and retry logic inside application and persistence code that can run under SQLite-backed tests.

The main product risk in this phase is accidental over-enrichment. Twilio Lookup v2 data packages are billable, so the default targeting query must strictly limit requests to never-enriched, failed, and stale-successful records. Full refresh should be an explicit operator choice, not the default path. Failure handling also needs to distinguish temporary provider/system problems from permanent classification outcomes so retries remain targeted and eligibility stays conservative.

**Primary recommendation:** Use a 3-plan sequence: first add enrichment schema and the Twilio Lookup adapter, then implement default/full-refresh enrichment workflow, then add failed-record review and retry actions.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Twilio .NET helper library | 7.14.3 | Official Twilio REST API client for Lookup requests from .NET 6+ apps | Official SDK, supports modern .NET, and keeps Twilio request signing and transport concerns out of app code. |
| Twilio Lookup v2 API | current | Validation plus Line Type Intelligence enrichment | Official Twilio endpoint for `valid`, `country_code`, and `line_type_intelligence` fields needed by this phase. |
| Microsoft.EntityFrameworkCore.SqlServer | 9.0.x | Persist latest enrichment snapshot and failure metadata | Already established as the system-of-record persistence layer in Phase 1. |
| Microsoft.EntityFrameworkCore.Sqlite | 9.0.10 | Fast relational verification of enrichment persistence | Matches the existing test strategy and avoids live SQL Server dependency in plan execution. |

### Supporting

| Library / Tool | Version | Purpose | When to Use |
|----------------|---------|---------|-------------|
| xUnit | existing project version | Unit and workflow tests | Continue using the existing test harness from Phase 1. |
| Twilio test credentials | current | Non-billed Lookup endpoint authentication | Useful for limited endpoint-path checks, but not sufficient for deterministic line-type package assertions. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Official Twilio SDK | Raw `HttpClient` calls | Raw HTTP gives tighter control, but duplicates auth, error handling, and response plumbing already covered by the Twilio helper library. |
| Latest-snapshot model | Full enrichment history table | A history table improves auditability, but the user explicitly chose latest-snapshot storage for Phase 2. |
| Live-provider integration tests | Fake lookup client + manual live check | Live tests prove real responses, but are costly and nondeterministic. Fakes plus one manual live verification are better for routine execution. |

**Installation:**
```bash
dotnet add src/PenelopeSMS.Infrastructure/PenelopeSMS.Infrastructure.csproj package Twilio --version 7.14.3
```

## Architecture Patterns

### Pattern 1: Twilio Lookup Adapter
**What:** Keep Twilio Lookup behind an interface such as `ITwilioLookupClient`, returning an internal result type that includes mapped facts, raw payload, and failure details.
**When to use:** Always for Twilio calls in this phase.
**Why:** Tests can use fakes while the infrastructure adapter owns Twilio SDK and response-shape details.

### Pattern 2: Latest Enrichment Snapshot on the Phone Record
**What:** Persist the latest successful enrichment facts and the latest failure metadata on `PhoneNumberRecord` (or a one-to-one enrichment component), not a historical attempts table.
**When to use:** For all Phase 2 storage changes.
**Why:** This aligns with the user’s decision to keep only the latest snapshot while still exposing retry and eligibility state.

### Pattern 3: Query-Driven Refresh Targeting
**What:** Build explicit selection logic for default mode, full refresh, retry-all retryable, and retry-selected operations.
**When to use:** Before the workflow starts making Twilio calls.
**Why:** Twilio Lookup data packages are billable; targeting logic is part of correctness, not an optimization.

### Pattern 4: Derived Eligibility Separate from Provider Facts
**What:** Store raw provider fields and compute eligibility into separate app-owned fields.
**When to use:** Every time enrichment updates a phone record.
**Why:** `ENRH-03` requires deriving eligibility without overwriting original provider facts.

## Twilio Lookup Findings

- Twilio Lookup v2 basic responses always include formatting and validation data such as `country_code`, `phone_number`, `valid`, and `validation_errors`.
- The `Fields` parameter must include `line_type_intelligence` to receive line-type and carrier details; otherwise that object is null.
- `line_type_intelligence` includes `carrier_name`, `mobile_country_code`, `mobile_network_code`, `type`, and `error_code`.
- Lookup data packages are billed features, so default selection logic should aggressively avoid unnecessary refreshes.
- Twilio test credentials support the Lookup endpoint itself. I infer from the docs that deterministic automated assertions for Line Type Intelligence still should not rely on live provider responses or undocumented magic numbers, so the test suite should primarily use fakes and reserve live Twilio verification for manual checks.

## Recommended Data Shape

The simplest phase-aligned schema extension is to add enrichment fields directly to `PhoneNumberRecord` (or a tightly coupled owned component) such as:

- `LastEnrichedAtUtc`
- `LastEnrichmentAttemptedAtUtc`
- `TwilioCountryCode`
- `TwilioLineType`
- `TwilioCarrierName`
- `TwilioMobileCountryCode`
- `TwilioMobileNetworkCode`
- `TwilioLookupPayloadJson`
- `IsCampaignEligible`
- `EligibilityEvaluatedAtUtc`
- `EnrichmentFailureStatus`
- `LastEnrichmentErrorCode`
- `LastEnrichmentErrorMessage`
- `RetryAfterUtc` or equivalent retryable marker

This model satisfies all four Phase 2 requirements without introducing history tables the user did not ask for.

## Failure Classification Guidance

Classify failures into two broad buckets:

- **Retryable:** temporary Twilio/provider/system faults such as transport failures, timeouts, 5xx responses, or package-level provider issues that indicate the lookup should be retried later.
- **Non-retryable:** valid-but-ineligible outcomes such as non-US country codes, non-mobile line types, invalid numbers, or unknown classifications after a successful response.

This keeps retries focused on real operational failures while preserving conservative eligibility behavior.

## Common Pitfalls

- **Calling Lookup without `line_type_intelligence`:** you will get basic validation but not the line type/carrier data required by `ENRH-02`.
- **Refreshing too broadly:** Twilio data packages are billable, so “refresh everything” as the default path is both costly and contrary to the user’s targeting decision.
- **Overwriting provider facts with eligibility results:** raw Twilio facts must remain queryable even after the app derives an eligibility flag.
- **Treating unknown classification as retryable by default:** the user explicitly decided unknown/unclassified outcomes are ineligible.
- **Making live Twilio calls part of routine tests:** provider responses and costs make that fragile; use fakes for the main suite.

## Code Examples

### Lookup request shape
```csharp
TwilioClient.Init(accountSid, authToken);

var phoneNumber = PhoneNumberResource.Fetch(
    pathPhoneNumber: "+14159929960",
    fields: "line_type_intelligence");
```

### Eligibility derivation rule
```csharp
var isEligible =
    lookup.Valid == true &&
    lookup.CountryCode == "US" &&
    string.Equals(lookup.LineType, "mobile", StringComparison.OrdinalIgnoreCase);
```

## Validation Architecture

- Continue using `xUnit` in `tests/PenelopeSMS.Tests/PenelopeSMS.Tests.csproj`.
- Use fake `ITwilioLookupClient` implementations for workflow, retry, and targeting tests.
- Use SQLite-backed persistence tests for schema and refresh-selection behavior.
- Keep fast feedback with `dotnet test PenelopeSMS.sln --no-restore` after each task.
- Add one manual live verification path for a known US mobile number and a known non-mobile/invalid number to confirm real Twilio payload mapping before phase sign-off.

## Sources

- Twilio Lookup v2 API: https://www.twilio.com/docs/lookup/v2-api
- Twilio Test Credentials: https://www.twilio.com/docs/iam/test-credentials
- Twilio .NET helper library repository: https://github.com/twilio/twilio-csharp
- Twilio NuGet package: https://www.nuget.org/packages/Twilio

---
*Phase research completed: 2026-03-12*
