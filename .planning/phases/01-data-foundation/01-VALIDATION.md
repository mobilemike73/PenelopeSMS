---
phase: 1
slug: data-foundation
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-03-12
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit |
| **Config file** | `tests/PenelopeSMS.Tests/PenelopeSMS.Tests.csproj` |
| **Quick run command** | `dotnet test PenelopeSMS.sln --no-restore` |
| **Full suite command** | `dotnet test PenelopeSMS.sln` |
| **Estimated runtime** | ~20 seconds |

## Sampling Rate

- **After every task commit:** Run `dotnet test PenelopeSMS.sln --no-restore`
- **After every plan wave:** Run `dotnet test PenelopeSMS.sln`
- **Before `$gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 1-01-01 | 01 | 1 | OPER-01 | build / smoke | `dotnet build PenelopeSMS.sln` | ❌ W0 | ⬜ pending |
| 1-01-02 | 01 | 1 | OPER-01 | unit | `dotnet test PenelopeSMS.sln --no-restore` | ❌ W0 | ⬜ pending |
| 1-02-01 | 02 | 2 | IMPT-02, IMPT-03 | unit | `dotnet test PenelopeSMS.sln --no-restore` | ❌ W0 | ⬜ pending |
| 1-02-02 | 02 | 2 | IMPT-04 | unit / relational | `dotnet test PenelopeSMS.sln --no-restore` | ❌ W0 | ⬜ pending |
| 1-03-01 | 03 | 3 | IMPT-01 | unit / integration-style | `dotnet test PenelopeSMS.sln --no-restore` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

## Wave 0 Requirements

- [ ] `tests/PenelopeSMS.Tests/PenelopeSMS.Tests.csproj` — create the xUnit test project
- [ ] `tests/PenelopeSMS.Tests/Host/HostBootstrapTests.cs` — smoke coverage for host and options binding
- [ ] `tests/PenelopeSMS.Tests/Data/PhoneNumberNormalizerTests.cs` — normalization and dedupe rule coverage
- [ ] `tests/PenelopeSMS.Tests/Import/ImportWorkflowTests.cs` — import orchestration coverage with a fake Oracle reader

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Real Oracle connection can execute the configured source query | IMPT-01 | Requires access to the actual Oracle environment and credentials | Run the import menu action with valid Oracle settings and confirm rows are imported into SQL Server |

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
