# Stack Research

**Domain:** Windows-only .NET 9 console application for phone import, number enrichment, and outbound SMS campaigns
**Researched:** 2026-03-12
**Confidence:** HIGH

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| .NET SDK / Runtime | 9.0 | Main application runtime | Current .NET release for this project; Generic Host works well in console apps and keeps configuration, logging, DI, and background workers consistent. |
| Microsoft.Extensions.Hosting | 9.x | Host, DI, config, logging, lifetime management | Microsoft recommends `Host.CreateApplicationBuilder` for new .NET apps, including console apps. |
| EF Core + SQL Server provider | 9.0.x | App database access and migrations for SQL Server Express | EF Core 9 matches .NET 9, and SQL Server remains the primary operational data store. |
| SQL Server Express | 2022 / local instance | System of record for imported numbers, campaigns, and delivery outcomes | Fits single-operator local deployment and works directly with EF Core SQL Server provider. |
| Oracle.ManagedDataAccess.Core | 23.26.100 | Read-only Oracle access for import pipeline | Keeps Oracle import isolated to a purpose-built provider instead of forcing dual-`DbContext` complexity into the app's primary model. |
| Twilio C# SDK | 7.14.3 | Twilio Lookup and Programmable Messaging API access | Official Twilio .NET SDK for number intelligence and outbound messaging. |
| AWS SDK for .NET SQS | 4.0.2.14 | Poll SQS for asynchronous delivery updates | Official AWS SDK package for long-poll queue consumption from the local app. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.EntityFrameworkCore.Design | 9.0.x | Migrations and design-time tooling | Use for schema evolution of the local SQL Server database. |
| Microsoft.Data.SqlClient | 6.x via EF provider | Direct SQL Server access when bulk operations need lower-level control | Use for targeted batch updates or bulk-write hot paths that are awkward in EF. |
| Twilio.AspNet.Core | 8.1.2 | Twilio request validation helpers for an ASP.NET Core callback bridge | Use only if you host your own HTTP callback endpoint instead of an AWS-only bridge. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| `dotnet-ef` | Manage EF Core migrations | Keep tool and package versions aligned with EF Core 9. |
| SQL Server Management Studio or Azure Data Studio | Inspect local SQL Server Express data | Useful for validating import, verification, and callback persistence. |
| Oracle SQL Developer | Validate Oracle source queries | Useful during import mapping and troubleshooting source data shape. |

## Installation

```bash
# Core
dotnet add package Microsoft.Extensions.Hosting --version 9.*
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 9.*
dotnet add package Microsoft.EntityFrameworkCore.Design --version 9.*
dotnet add package Oracle.ManagedDataAccess.Core --version 23.26.100
dotnet add package Twilio --version 7.14.3
dotnet add package AWSSDK.SQS --version 4.0.2.14

# Optional callback bridge
dotnet add package Twilio.AspNet.Core --version 8.1.2
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| ODP.NET Core for Oracle reads | Oracle EF Core provider | Use Oracle EF Core only if Oracle becomes a first-class domain model with significant LINQ/query composition needs. |
| Twilio Lookup Line Type Intelligence | Twilio Verify | Use Verify only for OTP / user-verification flows, not list enrichment or carrier/line-type classification. |
| Public callback bridge -> SQS -> local poller | Direct polling of Twilio Message SID statuses | Use direct polling only as a reconciliation fallback or for very low volume diagnostics. |
| API Gateway / Lambda / SQS callback ingestion | Public ASP.NET Core endpoint hosted by you | Use a self-hosted endpoint only if you already have a stable public deployment target and do not want AWS in the loop. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Twilio Verify for phone-type enrichment | Verify is for end-user verification flows; it does not match the app's need to classify imported numbers for marketing eligibility | Twilio Lookup with `line_type_intelligence` |
| A single EF Core model spanning both SQL Server and Oracle by default | Increases complexity, migrations risk, and provider-specific behavior for little value in a read-only import scenario | EF Core for SQL Server plus ODP.NET Core for Oracle import queries |
| Per-message status polling as the primary monitoring model | Creates unnecessary API churn and operational fragility at campaign scale | Status callbacks plus daily reconciliation polling for missed events |
| Public callback endpoints without signature validation | Twilio webhook endpoints are public and must be treated as hostile until validated | Validate `X-Twilio-Signature` and enforce HTTPS |

## Stack Patterns by Variant

**If keeping everything operator-local:**
- Use the console app plus SQL Server Express locally.
- Use AWS only for callback intake and queueing, then poll SQS from the local app.

**If you later add a small hosted component:**
- Keep campaign orchestration local.
- Host only a narrow callback bridge publicly, either in AWS serverless or a tiny ASP.NET Core endpoint.

**If callback hosting is only for development:**
- Use a tunneling tool only temporarily.
- Do not treat a transient tunnel as a production callback architecture.

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| .NET 9 | EF Core 9 | Natural pairing for new development. |
| EF Core SQL Server 9 | SQL Server 2022 / Express | Microsoft documents SQL Server provider support through `Microsoft.Data.SqlClient`. |
| Oracle.ManagedDataAccess.Core 23.26.100 | .NET Core / .NET modern runtimes | Current Oracle-managed provider for Oracle access on modern .NET. |
| Twilio 7.14.3 | .NET 9 app | Official client library for Lookup and Messaging REST APIs. |
| AWSSDK.SQS 4.0.2.14 | .NET 9 app | Current AWS SDK v4 package; use async receive/delete APIs with long polling. |

## Sources

- Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host — verified recommended Generic Host pattern for new console apps
- Microsoft Learn: https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-9.0/whatsnew — verified EF Core 9 release/support window
- Microsoft Learn: https://learn.microsoft.com/en-us/ef/core/providers/sql-server/ — verified SQL Server provider guidance
- Oracle docs: https://docs.oracle.com/en/database/oracle/oracle-database/26/odpnt/EFCore9features.html — verified Oracle EF Core 9 support notes and limitations
- NuGet: https://www.nuget.org/packages/Oracle.ManagedDataAccess.Core — verified current ODP.NET Core package version
- NuGet: https://www.nuget.org/packages/Twilio — verified current Twilio .NET SDK version
- NuGet: https://www.nuget.org/packages/AWSSDK.SQS — verified current AWS SQS SDK package version
- Twilio Docs: https://www.twilio.com/docs/lookup/v2-api and https://www.twilio.com/docs/lookup/v2-api/line-type-intelligence — verified Lookup capabilities and line type/carrier focus

---
*Stack research for: outbound SMS campaign console app*
*Researched: 2026-03-12*
