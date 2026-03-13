---
phase: 5
slug: operator-monitoring
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-12
---

# Phase 5 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit |
| **Config file** | `tests/PenelopeSMS.Tests/PenelopeSMS.Tests.csproj` |
| **Quick run command** | `dotnet test PenelopeSMS.sln --no-restore` |
| **Full suite command** | `dotnet test PenelopeSMS.sln` |
| **Estimated runtime** | ~35 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test PenelopeSMS.sln --no-restore`
- **After every plan wave:** Run `dotnet test PenelopeSMS.sln`
- **Before `$gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 35 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 5-01-01 | 01 | 1 | MON-04 | query / relational | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 5-01-02 | 01 | 1 | MON-04, OPER-02 | workflow / query | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 5-01-03 | 01 | 1 | MON-04 | unit / workflow | `dotnet test PenelopeSMS.sln` | ❌ planned | ⬜ pending |
| 5-02-01 | 02 | 2 | OPER-02 | service / unit | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 5-02-02 | 02 | 2 | OPER-02 | workflow / hosted-service | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 5-02-03 | 02 | 2 | OPER-02 | integration / runtime | `dotnet test PenelopeSMS.sln` | ❌ planned | ⬜ pending |
| 5-03-01 | 03 | 3 | MON-04, OPER-02 | menu / rendering | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 5-03-02 | 03 | 3 | MON-04 | workflow / integration | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 5-03-03 | 03 | 3 | MON-04, OPER-02 | end-to-end / manual-support | `dotnet test PenelopeSMS.sln` | ❌ planned | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing Generic Host, SQLite-backed relational tests, and xUnit coverage from Phases 1 through 4 already provide the needed validation infrastructure for monitoring queries, runtime services, and console workflows.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Shared monitoring screen remains readable while live jobs and delivery activity update | MON-04, OPER-02 | Console readability and refresh cadence are hard to judge from automated assertions alone | Start the app, open monitoring, run import/enrichment/send work, and confirm the screen repaints cleanly while warnings and live delivery lines remain understandable. |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 35s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
