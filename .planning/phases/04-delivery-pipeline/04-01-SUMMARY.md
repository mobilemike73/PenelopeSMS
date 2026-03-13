---
phase: 04-delivery-pipeline
plan: 01
subsystem: delivery
tags: [twilio, callbacks, aws, lambda, sqs]
requires:
  - phase: 03-03
    provides: outbound message SIDs and initial provider status persistence
provides:
  - delivery callback schema for status history plus unmatched and rejected events
  - Twilio webhook bridge that validates signatures and publishes normalized envelopes to SQS
  - configuration foundation for callback URL and callback DLQ settings
affects: [delivery-pipeline, campaign-execution, operator-monitoring]
tech-stack:
  added: [AWS Lambda HTTP API bridge]
  patterns: [callback envelope normalization, signature validation before queue publish, unmatched/rejected callback buckets]
key-files:
  created:
    - src/PenelopeSMS.Domain/Entities/CampaignRecipientStatusHistory.cs
    - src/PenelopeSMS.Domain/Entities/UnmatchedDeliveryCallback.cs
    - src/PenelopeSMS.Domain/Entities/RejectedDeliveryCallback.cs
    - src/PenelopeSMS.CallbackBridge/Functions/DeliveryStatusCallbackFunction.cs
    - tests/PenelopeSMS.Tests/Delivery/CallbackBridgeTests.cs
  modified:
    - src/PenelopeSMS.Domain/Entities/CampaignRecipient.cs
    - src/PenelopeSMS.Infrastructure/SqlServer/PenelopeSmsDbContext.cs
    - src/PenelopeSMS.App/Options/TwilioOptions.cs
    - src/PenelopeSMS.App/Options/AwsOptions.cs
key-decisions:
  - "Persist matched delivery history separately from unmatched and rejected callbacks so operator follow-up stays explicit."
  - "Validate Twilio webhook signatures in the public bridge before queueing any accepted callback envelope."
  - "Publish rejected callbacks into SQS too so local persistence captures malformed and invalid-signature traffic."
patterns-established:
  - "Pattern 1: callback ingestion uses API Gateway/Lambda style HTTP handling, normalizes payloads, and publishes one envelope per callback to SQS."
  - "Pattern 2: delivery state keeps both current-recipient fields and append-only history rows keyed by callback fingerprints."
requirements-completed: [MON-01]
duration: 14s
completed: 2026-03-12
---

# Phase 4 Plan 01 Summary

**Delivery callback schema and public bridge foundation**

## Accomplishments
- Added delivery ledger schema for current recipient state, status history, unmatched callbacks, and rejected callbacks.
- Added the Lambda callback bridge that validates `X-Twilio-Signature`, normalizes Twilio form posts, and publishes callback envelopes to SQS.
- Added options coverage for `Twilio:StatusCallbackUrl` and `Aws:CallbackDeadLetterQueueUrl`, plus automated bridge and schema tests.

## Task Commits
1. `07c8175` feat(04-01): add delivery callback persistence schema
2. `0232af6` feat(04-01): add Twilio callback bridge foundation
3. `62719f9` test(04-01): cover delivery schema and callback config

## Notes
- Duplicate fingerprints are enforced at the database level for history, unmatched, and rejected callback rows.
- The callback bridge accepts Twilio form posts and emits normalized JSON envelopes for the local SQS consumer.
- This plan established the persistence and ingress prerequisites for the local worker added in `04-02`.
