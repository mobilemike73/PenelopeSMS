# Phase 5: Operator Monitoring - Context

**Gathered:** 2026-03-12
**Status:** Ready for planning

<domain>
## Phase Boundary

Surface operator-facing visibility for campaign progress, job activity, and operational warnings inside the console app. This phase covers monitoring screens and console-visible runtime state for import, enrichment, sending, and delivery processing; new operational actions or remediation workflows remain outside this phase.

</domain>

<decisions>
## Implementation Decisions

### Campaign status view
- Monitoring should use a campaign summary list plus a drill-in screen for a selected campaign.
- The main campaign summary should show all tracked recipient states: `pending`, `submitted`, `queued`, `sent`, `delivered`, `undelivered`, and `failed`.
- Campaigns should sort by most recent activity, not by creation order.
- Completed campaigns should be hidden unless the operator explicitly requests them.

### Job visibility
- Use one shared monitoring view for import, enrichment, send, and delivery activity rather than separate top-level screens per job type.
- Monitoring views should auto-refresh while active.
- Completed jobs should stay summary-only rather than showing verbose detail inline.
- Delivery processing should appear as a persistent live status panel.

### Console signal and noise
- Monitoring should be summary-first by default, with detail available through drill-in rather than verbose output everywhere.
- Live delivery-processing logs should continue streaming rather than being reduced to stored snapshots only.
- Warnings should surface in a dedicated warnings section.
- Auto-refreshing screens should repaint in place instead of appending new screen blocks continuously.

### Failure surfacing
- Failures and warnings should be ordered newest first.
- Unmatched callbacks and rejected callbacks should appear together as one callback-issues bucket.
- Failure detail belongs in drill-in rather than on the main monitoring screen.
- Resolved issues should disappear from the main monitoring surface once they are no longer active.

### Claude's Discretion
- Exact menu names, key prompts, and screen layout structure for the monitoring views.
- Exact refresh cadence and terminal-width fallback behavior, as long as auto-refresh remains visible and readable.
- Exact definition of "recent activity" for campaign ordering, as long as it reflects meaningful operator-facing progress.

</decisions>

<specifics>
## Specific Ideas

- The operator wants monitoring to feel like one shared operational console rather than a collection of disconnected status commands.
- The live delivery panel should coexist with summary-first monitoring instead of taking over the entire screen.

</specifics>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope.

</deferred>

---

*Phase: 05-operator-monitoring*
*Context gathered: 2026-03-12*
