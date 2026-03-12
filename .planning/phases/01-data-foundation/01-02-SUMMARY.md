---
phase: 01-data-foundation
plan: 02
subsystem: database
tags: [ef-core, sql-server, sqlite, libphonenumber]
requires:
  - phase: 01-data-foundation
    provides: host bootstrap, typed configuration, test harness
provides:
  - Canonical phone, customer-link, and import-batch domain model
  - SQL Server EF Core schema and initial migration
  - SQLite-backed tests for normalization, dedupe, and batch audit behavior
affects: [import, database, testing]
tech-stack:
  added: [Microsoft.EntityFrameworkCore.SqlServer, Microsoft.EntityFrameworkCore.Design, Microsoft.EntityFrameworkCore.Sqlite, libphonenumber-csharp]
  patterns: [canonical phone plus association model, import batch ledger]
key-files:
  created:
    - src/PenelopeSMS.Domain/Entities/ImportBatch.cs
    - src/PenelopeSMS.Domain/Services/PhoneNumberNormalizer.cs
    - src/PenelopeSMS.Infrastructure/SqlServer/PenelopeSmsDbContext.cs
    - src/PenelopeSMS.Infrastructure/SqlServer/Repositories/ImportPersistenceService.cs
    - src/PenelopeSMS.Infrastructure/SqlServer/Migrations/20260312221801_InitialImportSchema.cs
    - tests/PenelopeSMS.Tests/Data/ImportPersistenceTests.cs
  modified:
    - src/PenelopeSMS.App/Program.cs
    - src/PenelopeSMS.Infrastructure/DependencyInjection.cs
    - src/PenelopeSMS.Infrastructure/PenelopeSMS.Infrastructure.csproj
    - tests/PenelopeSMS.Tests/PenelopeSMS.Tests.csproj
key-decisions:
  - "Store canonical phone numbers separately from customer associations so one phone row can map to multiple CUST_SID values."
  - "Use SQLite-backed tests for persistence behavior to keep plan verification independent from a local SQL Server instance."
patterns-established:
  - "Import batch ledger: each import run tracks timing, counts, and completion status."
  - "Persistence service upserts canonical phone rows before creating or updating customer links."
requirements-completed: [IMPT-02, IMPT-03, IMPT-04]
duration: 3 min
completed: 2026-03-12
---

# Phase 01: Data Foundation Summary

**Canonical phone persistence with EF Core migration, libphonenumber normalization, and SQLite-backed dedupe tests**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-12T18:17:25-04:00
- **Completed:** 2026-03-12T18:19:55-04:00
- **Tasks:** 3
- **Files modified:** 16

## Accomplishments
- Defined the canonical import domain model for phone numbers, `CUST_SID` links, and import-batch audit records.
- Added the SQL Server DbContext, entity mappings, import persistence service, and the initial EF Core migration.
- Covered normalization, duplicate-canonical handling, and import-batch audit updates with SQLite-backed automated tests.

## Task Commits

Each task was committed atomically:

1. **Task 1: Define the canonical import domain model and normalization service** - `5a5259b` (feat)
2. **Task 2: Build the SQL Server persistence layer and initial migration** - `4604c94` (feat)
3. **Task 3: Add automated tests for normalization, dedupe, and import-audit persistence** - `45957af` (feat)

**Plan metadata:** Recorded in the `docs(01-02)` completion commit for this summary, state update, and roadmap progress change.

## Files Created/Modified
- `src/PenelopeSMS.Domain/Entities/*.cs` - Canonical phone, customer-link, and import-batch entities.
- `src/PenelopeSMS.Domain/Services/PhoneNumberNormalizer.cs` - E.164 normalization using `libphonenumber-csharp`.
- `src/PenelopeSMS.Infrastructure/SqlServer/PenelopeSmsDbContext.cs` - EF Core DbContext for the Phase 1 schema.
- `src/PenelopeSMS.Infrastructure/SqlServer/Configurations/*.cs` - Relational mappings, lengths, indexes, and relationships.
- `src/PenelopeSMS.Infrastructure/SqlServer/Repositories/ImportPersistenceService.cs` - Batch creation, canonical upsert, and customer-link persistence.
- `src/PenelopeSMS.Infrastructure/SqlServer/Migrations/*InitialImportSchema*.cs` - Initial SQL Server schema migration and model snapshot.
- `tests/PenelopeSMS.Tests/Data/*.cs` - Domain and relational persistence coverage.

## Decisions Made
- Kept canonical phone rows globally unique by E.164 number and modeled `CUST_SID` relationships separately to preserve duplicate lineage.
- Stored raw phone input on the customer-link row so source formatting is retained for diagnostics without duplicating canonical phone records.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Adjusted generated migration to satisfy repository analyzers**
- **Found during:** Task 2 (Build the SQL Server persistence layer and initial migration)
- **Issue:** The generated EF migration introduced a CA1861 analyzer warning, which would have violated the plan's zero-warning requirement.
- **Fix:** Replaced the repeated inline column array with a static readonly field in the generated migration.
- **Files modified:** src/PenelopeSMS.Infrastructure/SqlServer/Migrations/20260312221801_InitialImportSchema.cs
- **Verification:** `dotnet test PenelopeSMS.sln --no-restore`
- **Committed in:** 4604c94 (part of task commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** The fix preserved the generated schema while keeping the solution warning-free.

## Issues Encountered
- Running `dotnet ef` through the WSL-hosted `dotnet.exe` failed with a CLI runtime error, so the migration was generated via `cmd.exe` using the same Windows SDK and the infrastructure project as the startup target.

## User Setup Required

None - no new external service configuration was required for this plan.

## Next Phase Readiness
- The Oracle import workflow can now persist canonical numbers, duplicate associations, and batch audit counts through a tested persistence layer.
- No blockers remain for wiring the read-only Oracle adapter and menu-driven import flow in plan 01-03.

---
*Phase: 01-data-foundation*
*Completed: 2026-03-12*
