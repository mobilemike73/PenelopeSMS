---
phase: 04-delivery-pipeline
plan: 03
subsystem: delivery
tags: [twilio, host, runtime, docs]
requires:
  - phase: 04-02
    provides: local callback worker and delivery-processing workflow
provides:
  - outbound Twilio sends with configured `StatusCallback`
  - automatic hosted startup for callback processing while the console app is open
  - deployment and operator setup guidance for Lambda, API Gateway, SQS, and local configuration
affects: [delivery-pipeline, campaign-execution, operator-monitoring]
tech-stack:
  added: [Lambda deployment defaults]
  patterns: [callback-aware send configuration, hosted worker startup, bridge-to-worker runtime verification]
key-files:
  created:
    - tests/PenelopeSMS.Tests/Delivery/DeliveryRuntimeIntegrationTests.cs
    - src/PenelopeSMS.CallbackBridge/README.md
    - .planning/phases/04-delivery-pipeline/04-USER-SETUP.md
  modified:
    - src/PenelopeSMS.Infrastructure/Twilio/TwilioMessageSender.cs
    - src/PenelopeSMS.App/Program.cs
    - src/PenelopeSMS.App/Menu/CampaignMenuAction.cs
    - src/PenelopeSMS.App/Services/DeliveryCallbackWorker.cs
key-decisions:
  - "Attach the callback URL from `Twilio:StatusCallbackUrl` on each outbound message instead of relying on Twilio console defaults."
  - "Start callback processing automatically with the host rather than through a separate menu action."
  - "Support existing API Gateway reuse through a dedicated public route or path mapping rather than requiring a separate gateway."
patterns-established:
  - "Pattern 1: hosted background services that need scoped persistence dependencies resolve their workflows through a scope factory per iteration."
  - "Pattern 2: runtime integration tests can validate bridge-envelope compatibility by serializing the callback bridge model and feeding it through the worker."
requirements-completed: [MON-01, MON-02, MON-03]
duration: 10s
completed: 2026-03-12
---

# Phase 4 Plan 03 Summary

**Outbound callback wiring, automatic startup, and operator setup**

## Accomplishments
- Wired outbound Twilio sends to attach the configured callback URL and fail fast on invalid callback URI configuration.
- Registered the delivery worker as a hosted service, started the host automatically in `Program.Main`, and added runtime-facing campaign callback messaging.
- Added runtime integration coverage plus deployment/setup docs for the Lambda bridge, SQS queue and DLQ, Twilio callback URL, and API Gateway reuse.

## Task Commits
1. `000a868` feat(04-03): wire callback-aware sends and host startup
2. `24c1705` test(04-03): add runtime delivery pipeline coverage
3. `85b3bfb` docs(04-03): add callback bridge deployment setup

## Notes
- The hosted worker now resolves scoped processing dependencies per queue iteration, which keeps `DbContext` lifetime correct.
- Campaign send output warns when `Twilio:StatusCallbackUrl` is blank so operators do not assume the delivery ledger is active.
- The user setup doc reflects the approved choice to reuse an existing API Gateway through a dedicated route or path mapping.
