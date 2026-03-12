# Phase 1: Data Foundation - Research

**Researched:** 2026-03-12
**Domain:** .NET 9 console app foundation, SQL Server persistence, Oracle import, and canonical phone-number modeling
**Confidence:** HIGH

## User Constraints

No `CONTEXT.md` was provided for this phase. Planning is constrained by the project artifacts already approved:

- Windows-only `.NET 9` console application
- Local SQL Server Express at `.\SQLEXPRESS` / `127.0.0.1,1433`
- Oracle is a read-only source system for import
- Single internal operator, no auth, no GUI
- Twilio provisioning / 10DLC setup is already complete and out of scope

## Summary

Phase 1 should establish one hosted console solution with a clear split between app orchestration, domain rules, and infrastructure adapters. The highest-leverage implementation choice is to keep SQL Server as the only EF Core model and use Oracle's managed provider strictly for import queries. That keeps schema migrations and local persistence straightforward while avoiding dual-provider ORM complexity in the first phase.

The most important data-model decision in this phase is to separate canonical phone numbers from source relationships. `CUST_SID` should not live on the canonical number row by itself; instead, the schema should represent one normalized phone record with one-to-many source associations so duplicate numbers do not lead to duplicate messaging later.

The highest-risk failure mode is building import logic before the schema, normalization, and tests are in place. The plans should therefore front-load solution bootstrap and test infrastructure, then create the database model and canonicalization rules, and only then wire the Oracle import workflow into the console shell.

**Primary recommendation:** Use a 3-plan sequence: bootstrap host and config, implement the SQL Server model and normalization logic, then wire the Oracle import workflow on top.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK / Runtime | 9.0 | Console app runtime | Current platform target for the project and the natural base for Generic Host patterns. |
| Microsoft.Extensions.Hosting | 9.x | DI, config, logging, background orchestration | Recommended Microsoft pattern for modern console apps. |
| Microsoft.EntityFrameworkCore.SqlServer | 9.0.x | SQL Server Express persistence and migrations | Primary provider for the local system-of-record database. |
| Oracle.ManagedDataAccess.Core | 23.26.100 | Oracle import connectivity | Official managed Oracle provider for modern .NET. |
| libphonenumber-csharp | 9.0.24 | Canonical phone-number parsing and normalization | Avoids hand-rolled phone normalization and supports line-type-aware number parsing. |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.EntityFrameworkCore.Design | 9.0.x | Migrations tooling | Use for initial SQL Server schema and later migrations. |
| Microsoft.EntityFrameworkCore.Sqlite | 9.0.10 | Fast in-memory relational testing | Use in tests to exercise EF mappings and repository logic without depending on local SQL Server. |
| xUnit | current `dotnet new xunit` template | Automated unit/integration-style tests | Use for phase verification and Nyquist compliance. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| ODP.NET Core for Oracle reads | Oracle EF Core provider | Oracle EF Core is viable if Oracle becomes a first-class domain model, but it adds complexity that this read-only import phase does not need. |
| libphonenumber-csharp | Minimal custom E.164 formatter | A tiny formatter is simpler short-term, but easier to get wrong and harder to extend safely. |
| SQL Server-backed tests only | SQLite-backed relational tests | Real SQL Server tests are closer to production, but SQLite tests are faster and easier for phase-level automation. |

**Installation:**
```bash
dotnet add src/PenelopeSMS.App/PenelopeSMS.App.csproj package Microsoft.Extensions.Hosting --version 9.*
dotnet add src/PenelopeSMS.Infrastructure/PenelopeSMS.Infrastructure.csproj package Microsoft.EntityFrameworkCore.SqlServer --version 9.*
dotnet add src/PenelopeSMS.Infrastructure/PenelopeSMS.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design --version 9.*
dotnet add src/PenelopeSMS.Infrastructure/PenelopeSMS.Infrastructure.csproj package Oracle.ManagedDataAccess.Core --version 23.26.100
dotnet add src/PenelopeSMS.Domain/PenelopeSMS.Domain.csproj package libphonenumber-csharp --version 9.0.24
dotnet add tests/PenelopeSMS.Tests/PenelopeSMS.Tests.csproj package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.10
```

## Architecture Patterns

### Recommended Project Structure

```text
src/
├── PenelopeSMS.App/                # Host bootstrap, menu actions, workflows, options binding
├── PenelopeSMS.Domain/             # Entities, value objects, normalization rules
├── PenelopeSMS.Infrastructure/     # SQL Server, Oracle, and integration adapters
│   ├── SqlServer/
│   └── Oracle/
└── tests/PenelopeSMS.Tests/        # xUnit tests for domain and infrastructure behavior
```

### Pattern 1: Hosted Console Composition Root
**What:** One console entry point creates a host, binds options, registers services, and hands control to a menu workflow.
**When to use:** Always for this app; it keeps later background work and dependencies consistent.
**Example:**
```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<SqlServerOptions>(builder.Configuration.GetSection("SqlServer"));
builder.Services.AddScoped<IImportWorkflow, ImportWorkflow>();
using var host = builder.Build();
await host.Services.GetRequiredService<IMainMenu>().RunAsync();
```

### Pattern 2: Canonical Number + Association Model
**What:** Store one normalized phone entity and a separate join entity for each `CUST_SID` relationship.
**When to use:** Whenever the same phone can appear for more than one source customer.
**Example:**
```csharp
PhoneNumberRecord { Id, E164Number, RawInput, LastImportedAt }
CustomerPhoneLink { Id, CustSid, PhoneNumberRecordId, ImportBatchId }
```

### Pattern 3: Import Batch Ledger
**What:** Persist each import attempt and its counts so imports are auditable and restart-safe.
**When to use:** Always for Phase 1 because import observability is a shipped requirement.
**Example:**
```csharp
ImportBatch { StartedAtUtc, CompletedAtUtc, RowsRead, RowsImported, RowsRejected, Status }
```

### Anti-Patterns to Avoid
- **Dual-provider domain model from day one:** keep Oracle read-only and thin.
- **Phone normalization by regex only:** use a phone parsing library unless the source data is guaranteed perfect.
- **Import logic that writes directly from Oracle rows into message-ready tables:** enforce canonicalization and association tracking first.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Option binding | Custom config parsing | `IOptions<T>` / configuration binding | Keeps environment-specific configuration clean and testable |
| Phone normalization | Regex-only parsing | `libphonenumber-csharp` | Better handling of country/format edge cases |
| ORM for Oracle import domain | Second rich EF model | ODP.NET Core query adapter | Less complexity for a read-only source |
| Relational test harness | Hand-made fake repositories only | SQLite-backed EF tests plus focused fakes | Better confidence in mappings and dedupe behavior |

## Common Pitfalls

- **Losing duplicate lineage:** if `CUST_SID` is stored only on the canonical phone row, duplicate relationships disappear.
- **Schema after workflow:** if the import workflow is built before the schema and tests, rework is almost guaranteed.
- **Config baked into code:** hard-coded server names or credentials will immediately violate `OPER-01`.
- **Missing import audit data:** import counts and error tracking are required phase outputs, not nice-to-haves.

## Code Examples

### Options Binding Pattern
```csharp
builder.Services
    .AddOptions<OracleOptions>()
    .Bind(builder.Configuration.GetSection("Oracle"))
    .ValidateDataAnnotations();
```

### Canonicalization Service Shape
```csharp
public interface IPhoneNumberNormalizer
{
    NormalizedPhoneNumber Normalize(string rawPhoneNumber, string defaultRegion);
}
```

## Validation Architecture

- Use `xUnit` for automated tests created in the first plan of the phase.
- Use `Microsoft.EntityFrameworkCore.Sqlite` in tests to validate relational mappings and dedupe rules without a live SQL Server dependency.
- Keep verification commands fast: `dotnet build PenelopeSMS.sln` and `dotnet test PenelopeSMS.sln`.
- Treat real Oracle connectivity as a manual-environment check; automate import behavior through adapter interfaces and fake Oracle rows.

## Sources

- https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host
- https://learn.microsoft.com/en-us/ef/core/providers/sql-server/
- https://docs.oracle.com/en/database/oracle/oracle-database/26/odpnt/EFCore9features.html
- https://www.nuget.org/packages/Oracle.ManagedDataAccess.Core
- https://www.nuget.org/packages/libphonenumber-csharp/
- https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite/9.0.10

---
*Phase research completed: 2026-03-12*
