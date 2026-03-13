# Phase 4: Delivery Pipeline - Context

**Gathered:** 2026-03-12
**Status:** Ready for planning

<domain>
## Phase Boundary

Ingest Twilio delivery callbacks through the configured callback path, consume them from AWS SQS, and apply delivery-status history to the local outbound message ledger. This phase covers callback intake, queue consumption, message-status history, and invalid-event handling; broader operator monitoring and campaign dashboards remain outside this phase.

</domain>

<decisions>
## Implementation Decisions

### Delivery statuses and history detail
- Treat `queued`, `sent`, `delivered`, `undelivered`, and `failed` as the first-class SMS delivery states for v1.
- Store all callback details on each persisted history entry: Twilio event timestamp, app processed timestamp, provider error code, provider error message, and a raw payload snapshot.
- Collapse repeated callbacks when nothing meaningful changed instead of storing identical duplicates.
- Determine the current message status from the latest Twilio event time, not simply the latest callback arrival time.

### Unknown or invalid callback handling
- Callbacks that reference an unknown Twilio Message SID go into an unmatched-events bucket.
- Malformed payloads fail that event only; processing continues for the rest of the queue.
- Signature/authenticity failures are handled separately from malformed payloads.
- Invalid or unknown events should raise console warnings in v1.

### Duplicate and out-of-order event behavior
- When a duplicate callback arrives with the same status and no meaningful new details, update a `last seen` timestamp on the collapsed entry.
- Discard older events once a newer event has already been applied.
- Current message status still follows the latest Twilio event time.
- Late non-terminal callbacks such as `sent` should not be kept after a terminal outcome has already been recorded.

### Callback-to-queue flow expectations
- Use a simple sequential worker for v1 queue processing.
- Failed SQS processing should move toward a dead-letter path.
- Callback processing should run continuously while the app is open.
- Console output for callback processing should be verbose.

### Claude's Discretion
- Exact schema shape for unmatched-event storage, collapsed-history bookkeeping, and callback-history tables.
- Exact console wording and progress formatting, as long as warnings and verbose processing output remain visible.
- Exact retry/dead-letter thresholds and queue-poll timing, as long as the dead-letter path is the default failure direction.

</decisions>

<specifics>
## Specific Ideas

- The accepted recommendations in this context are: first-class status history for `queued`, `sent`, `delivered`, `undelivered`, and `failed`, plus a sequential SQS worker for the initial implementation.
- The operator wants invalid and unknown callbacks surfaced visibly in the console rather than silently ignored.

</specifics>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope.

</deferred>

---

*Phase: 04-delivery-pipeline*
*Context gathered: 2026-03-12*
