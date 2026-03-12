---
phase: 2
slug: number-intelligence
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-12
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit |
| **Config file** | `tests/PenelopeSMS.Tests/PenelopeSMS.Tests.csproj` |
| **Quick run command** | `dotnet test PenelopeSMS.sln --no-restore` |
| **Full suite command** | `dotnet test PenelopeSMS.sln` |
| **Estimated runtime** | ~25 seconds |

## Sampling Rate

- **After every task commit:** Run `dotnet test PenelopeSMS.sln --no-restore`
- **After every plan wave:** Run `dotnet test PenelopeSMS.sln`
- **Before `$gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 2-01-01 | 01 | 1 | ENRH-02, ENRH-03, ENRH-04 | unit / relational | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 2-01-02 | 01 | 1 | ENRH-02 | unit | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 2-02-01 | 02 | 2 | ENRH-01 | unit / query | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 2-02-02 | 02 | 2 | ENRH-01, ENRH-02, ENRH-03, ENRH-04 | workflow | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 2-03-01 | 03 | 3 | ENRH-04 | workflow / console | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

## Wave 0 Requirements

Existing infrastructure covers all phase requirements.

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Real Twilio Lookup response maps line type and carrier facts correctly | ENRH-02 | Provider responses are billable and not deterministic enough for routine automated tests | Run the enrichment workflow against one known US mobile number and one known ineligible number with live Twilio credentials, then confirm the stored snapshot and eligibility fields in SQL Server. |
| Default run targeting refreshes only due records in a real environment | ENRH-01 | Depends on live Twilio credentials and real stored data timing | Seed a mix of never-enriched, failed, stale-success, and fresh-success records; run the default enrichment action and verify only due records were queried. |

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
