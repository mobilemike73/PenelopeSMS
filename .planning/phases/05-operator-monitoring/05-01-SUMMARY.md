---
phase: 05-operator-monitoring
plan: 01
subsystem: monitoring
tags: [sql, queries, read-models, campaigns]
requires:
  - phase: 04-03
    provides: persisted campaign and delivery state plus hosted callback processing
provides:
  - campaign monitoring read models with recent-activity ordering and full recipient status totals
  - persisted issue summaries for callback issues, enrichment failures, and failed imports
  - workflow-facing dashboard and drill-in records for the operator monitor
affects: [operator-monitoring, campaign-execution, delivery-pipeline]
tech-stack:
  added: [monitoring query layer]
  patterns: [SQL-backed dashboard read models, persisted issue aggregation, summary-plus-drill-in workflow shape]
key-files:
  created:
    - src/PenelopeSMS.Infrastructure/SqlServer/Queries/CampaignMonitoringQuery.cs
    - src/PenelopeSMS.Infrastructure/SqlServer/Queries/OperationsIssueQuery.cs
    - tests/PenelopeSMS.Tests/Monitoring/CampaignMonitoringQueryTests.cs
  modified:
    - src/PenelopeSMS.App/Workflows/IMonitoringWorkflow.cs
    - src/PenelopeSMS.App/Workflows/MonitoringWorkflow.cs
    - src/PenelopeSMS.Infrastructure/DependencyInjection.cs
    - tests/PenelopeSMS.Tests/Monitoring/MonitoringWorkflowTests.cs
key-decisions:
  - "Order campaigns by last meaningful activity instead of creation time so the monitor reflects current operator attention."
  - "Aggregate unmatched and rejected callbacks into one callback-issues bucket on the main screen."
  - "Keep recent recipient failures out of the main list and expose them through campaign drill-in."
patterns-established:
  - "Pattern 1: operator-facing monitoring reads from dedicated query models instead of reusing write-side repositories."
  - "Pattern 2: monitoring workflows return summary-first snapshots with explicit drill-in records for detailed failure samples."
requirements-completed: [MON-04]
duration: 12m
completed: 2026-03-13
---

# Phase 5 Plan 01 Summary

**Monitoring read models and persisted dashboard foundations**

## Accomplishments
- Added SQL-backed campaign monitoring summaries with full status totals and recent-activity ordering.
- Added persisted issue aggregation for callback issues, enrichment failures, and failed import batches.
- Added workflow snapshot types that support the shared monitoring dashboard and campaign drill-in path.

## Task Commits
1. `9ea77cf` feat(05-01): add campaign monitoring query models
2. `45cf665` feat(05-01): add monitoring workflow issue summaries
3. `f0a49c4` test(05-01): cover monitoring query rules

## Notes
- Completed campaigns stay filterable at the workflow boundary so the UI can hide them by default.
- The persisted issue model keeps callback issues grouped while still retaining detailed counts for unmatched versus rejected callbacks.
- This plan established the database-backed side of the Phase 5 dashboard before live runtime state was added in `05-02`.
