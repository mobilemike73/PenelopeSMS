---
phase: 4
slug: delivery-pipeline
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-12
---

# Phase 4 — Validation Strategy

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
| 4-01-01 | 01 | 1 | MON-01, MON-03 | unit / schema | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 4-01-02 | 01 | 1 | MON-01 | bridge / unit | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 4-01-03 | 01 | 1 | MON-01 | adapter / config | `dotnet test PenelopeSMS.sln` | ❌ planned | ⬜ pending |
| 4-02-01 | 02 | 2 | MON-02, MON-03 | repository / workflow | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 4-02-02 | 02 | 2 | MON-02 | worker / hosted-service | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 4-02-03 | 02 | 2 | MON-02, MON-03 | unit / relational | `dotnet test PenelopeSMS.sln` | ❌ planned | ⬜ pending |
| 4-03-01 | 03 | 3 | MON-01 | adapter / workflow | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 4-03-02 | 03 | 3 | MON-02, MON-03 | host / integration | `dotnet test PenelopeSMS.sln --no-restore` | ❌ planned | ⬜ pending |
| 4-03-03 | 03 | 3 | MON-01, MON-02, MON-03 | end-to-end / manual-support | `dotnet test PenelopeSMS.sln` | ❌ planned | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing Generic Host, SQL Server persistence, Twilio send adapter, and AWS options binding infrastructure from Phases 1 through 3 already cover the base test harness and app composition needs for this phase.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Twilio can reach the deployed callback bridge and the bridge forwards a validated event into SQS | MON-01 | Requires a public URL, real Twilio traffic, and deployed AWS infrastructure | Send a real SMS with the configured `StatusCallback` URL, confirm the bridge logs a valid request, and verify the normalized callback appears in the SQS queue. |
| The local worker applies a real delivery callback into SQL Server with correct current state and history entries | MON-02, MON-03 | Requires live Twilio status callbacks plus real AWS queue delivery timing | With the app running, process a real queued-to-delivered SMS callback sequence and confirm SQL Server shows the expected current status, history row(s), and verbose console output. |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 35s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
