---
phase: 3
slug: campaign-execution
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-12
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit |
| **Config file** | `tests/PenelopeSMS.Tests/PenelopeSMS.Tests.csproj` |
| **Quick run command** | `dotnet test PenelopeSMS.sln --no-restore` |
| **Full suite command** | `dotnet test PenelopeSMS.sln` |
| **Estimated runtime** | ~30 seconds |

## Sampling Rate

- **After every task commit:** Run `dotnet test PenelopeSMS.sln --no-restore`
- **After every plan wave:** Run `dotnet test PenelopeSMS.sln`
- **Before `$gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 3-01-01 | 01 | 1 | CAMP-01, CAMP-04, CAMP-05, CAMP-06 | unit / relational | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 3-01-02 | 01 | 1 | CAMP-01 | unit | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 3-02-01 | 02 | 2 | CAMP-01, CAMP-02, CAMP-03, CAMP-06 | workflow / persistence | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 3-02-02 | 02 | 2 | CAMP-02, CAMP-03, CAMP-06 | workflow / console | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 3-03-01 | 03 | 3 | CAMP-03, CAMP-04, CAMP-05 | workflow / adapter | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 3-03-02 | 03 | 3 | CAMP-04, CAMP-05 | workflow / persistence | `dotnet test PenelopeSMS.sln` | ❌ planned | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

## Wave 0 Requirements

Existing host, SQL Server, enrichment eligibility, and Twilio credentials infrastructure are already in place from Phases 1 and 2.

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Real Twilio send returns a Message SID and initial status through the configured Messaging Service | CAMP-04, CAMP-05 | Provider responses are paid and environment-specific | Create a small campaign with a safe test number, run one batch send with live credentials, then confirm the stored Message SID and initial provider status in SQL Server. |
| Operator-facing template file flow works with a real file path on disk | CAMP-01 | File selection and operator paths are environment-specific | Create a `.txt` template on disk, draft a campaign from the console, and confirm the campaign snapshot stores both file path and body text. |

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
