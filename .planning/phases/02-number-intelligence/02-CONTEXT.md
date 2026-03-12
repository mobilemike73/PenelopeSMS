# Phase 2: Number Intelligence - Context

**Gathered:** 2026-03-12
**Status:** Ready for planning

<domain>
## Phase Boundary

Enrich imported phone records with Twilio provider data and derive campaign eligibility from the stored enrichment result. This phase covers enrichment runs, stored provider facts, eligibility derivation, and enrichment failure visibility; campaign sending and other new capabilities remain outside this phase.

</domain>

<decisions>
## Implementation Decisions

### Eligibility rule
- Only numbers classified as `mobile` are campaign-eligible.
- Numbers with unclassified, unknown, or uncertain enrichment results are ineligible.
- Eligibility is limited to `US` numbers.
- The latest enrichment result determines current eligibility.

### Refresh targeting
- The default enrichment run targets phone records that have never been enriched plus records with failed enrichment attempts.
- Previously successful enrichments become due for refresh after `30 days`.
- The console flow should offer a default enrichment mode plus a separate full-refresh option.

### Stored provider facts
- Store mapped Twilio fields plus the full raw provider payload.
- Carrier data is optional; keep it when present and leave it null when absent.
- Keep only the latest successful enrichment snapshot on the phone record for Phase 2.

### Failure and retry visibility
- Retryable failures are limited to temporary provider or system failures.
- Non-retryable enrichment failures make the phone record ineligible.
- After a run, show summary counts plus a failed-record list.
- Provide both `retry all retryable` and `retry selected` actions in the console workflow.

### Claude's Discretion
- Exact console wording, menu layout, and summary formatting.
- Exact internal categorization logic for transient/provider/system failures, as long as it matches the retryable/non-retryable split above.
- Exact schema shape for storing the latest provider snapshot and raw payload, as long as the required facts remain queryable.

</decisions>

<specifics>
## Specific Ideas

No specific product or UI references were provided. Use standard console-first patterns that make default enrichment, full refresh, failure review, and retry actions clear to a single operator.

</specifics>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope.

</deferred>

---

*Phase: 02-number-intelligence*
*Context gathered: 2026-03-12*
