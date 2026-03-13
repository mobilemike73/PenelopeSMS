---
phase: 04-delivery-pipeline
plan: 02
subsystem: delivery
tags: [sqs, worker, sql-server, idempotency]
requires:
  - phase: 04-01
    provides: normalized callback envelopes and delivery callback schema
provides:
  - sequential SQS receive/delete abstraction
  - idempotent delivery callback application with duplicate collapse and out-of-order protection
  - local worker and workflow tests for queue deletion safety
affects: [delivery-pipeline, operator-monitoring]
tech-stack:
  added: [AWS SQS client abstraction]
  patterns: [best-available event time precedence, duplicate last-seen updates, delete-after-persist queue handling]
key-files:
  created:
    - src/PenelopeSMS.Infrastructure/Aws/AwsSqsClient.cs
    - src/PenelopeSMS.Infrastructure/SqlServer/Repositories/DeliveryCallbackRepository.cs
    - src/PenelopeSMS.App/Services/DeliveryCallbackWorker.cs
    - src/PenelopeSMS.App/Workflows/DeliveryCallbackProcessingWorkflow.cs
    - tests/PenelopeSMS.Tests/Delivery/DeliveryCallbackRepositoryTests.cs
  modified:
    - src/PenelopeSMS.Infrastructure/DependencyInjection.cs
    - src/PenelopeSMS.App/Program.cs
key-decisions:
  - "Callback application chooses the best available event time, using `RawDlrDoneDate` when available and callback receipt time otherwise."
  - "Exact duplicate callbacks update `LastSeenAtUtc` on the existing row instead of appending new history."
  - "Processing failures do not delete SQS messages so retry and DLQ behavior remain the default failure path."
patterns-established:
  - "Pattern 1: queue workers process one SQS message at a time and only delete after persistence reports success."
  - "Pattern 2: delivery repositories protect current state from older event times while still tracking last callback receipt."
requirements-completed: [MON-02, MON-03]
duration: 10s
completed: 2026-03-12
---

# Phase 4 Plan 02 Summary

**Sequential SQS processing and idempotent callback application**

## Accomplishments
- Added the AWS SQS client abstraction used for long polling and explicit post-persistence deletes.
- Added idempotent callback persistence logic for matched, duplicate, older, unmatched, and rejected callback paths.
- Added the delivery callback processing workflow and worker coverage proving malformed messages, unsupported statuses, duplicates, and worker delete semantics.

## Task Commits
1. `b7caa06` feat(04-02): add delivery callback persistence workflow
2. `21d2c4e` feat(04-02): add sequential delivery callback worker
3. `07cf8b1` test(04-02): cover delivery callback processing rules

## Notes
- Unsupported delivery statuses are persisted through the rejected-callback path instead of poisoning the queue.
- Older callbacks do not overwrite current recipient state, but duplicate and discarded events still update last-seen receipt timestamps where applicable.
- The worker remains sequential by design for v1.
