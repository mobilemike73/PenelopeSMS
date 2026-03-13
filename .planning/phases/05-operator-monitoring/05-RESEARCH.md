# Phase 5: Operator Monitoring - Research

**Researched:** 2026-03-12
**Domain:** Console monitoring for campaign progress, shared job visibility, runtime warnings, and live delivery activity
**Confidence:** HIGH

## User Constraints

### Locked Decisions From Context

#### Campaign status view
- Monitoring should use a campaign summary list plus a drill-in screen for a selected campaign.
- The main campaign summary should show all tracked recipient states: `pending`, `submitted`, `queued`, `sent`, `delivered`, `undelivered`, and `failed`.
- Campaigns should sort by most recent activity, not by creation order.
- Completed campaigns should be hidden unless the operator explicitly requests them.

#### Job visibility
- Use one shared monitoring view for import, enrichment, send, and delivery activity rather than separate top-level screens per job type.
- Monitoring views should auto-refresh while active.
- Completed jobs should stay summary-only rather than showing verbose detail inline.
- Delivery processing should appear as a persistent live status panel.

#### Console signal and noise
- Monitoring should be summary-first by default, with detail available through drill-in rather than verbose output everywhere.
- Live delivery-processing logs should continue streaming rather than being reduced to stored snapshots only.
- Warnings should surface in a dedicated warnings section.
- Auto-refreshing screens should repaint in place instead of appending new screen blocks continuously.

#### Failure surfacing
- Failures and warnings should be ordered newest first.
- Unmatched callbacks and rejected callbacks should appear together as one callback-issues bucket.
- Failure detail belongs in drill-in rather than on the main monitoring screen.
- Resolved issues should disappear from the main monitoring surface once they are no longer active.

#### Claude's Discretion
- Exact menu names, key prompts, and screen layout structure for the monitoring views.
- Exact refresh cadence and terminal-width fallback behavior, as long as auto-refresh remains visible and readable.
- Exact definition of "recent activity" for campaign ordering, as long as it reflects meaningful operator-facing progress.

#### Deferred Ideas

None - discussion stayed within phase scope.

## Summary

Phase 5 should not bolt monitoring onto the current ad hoc `Console.WriteLine` calls. The codebase already persists most of the durable state needed for operator visibility: import batches, enrichment failures and eligibility, campaign and recipient statuses, delivery history, unmatched callbacks, and rejected callbacks. What is missing is a monitoring-specific read model plus a small runtime state service for live jobs and warnings.

The clean split is:
- SQL-backed read queries for campaign summaries, campaign drill-in details, and persisted callback/enrichment/import issue summaries
- a singleton in-memory operations monitor that tracks currently running jobs, recent completed job summaries, warning entries, and live delivery activity lines
- a monitoring screen renderer that repaints in place on a timer while reading from those snapshots instead of allowing concurrent background writes to scribble across the terminal

This phase should stay inside the existing console stack. Adding a full console UI framework late in the milestone would expand the surface area too much for limited gain. The app already uses a simple menu model under Generic Host, so the standard approach is to add a `Monitoring` menu entry, build snapshot-oriented workflows for the new read models, and use a single rendering loop with `PeriodicTimer` plus explicit screen clearing or cursor repositioning. The background delivery worker and foreground workflows should still emit live entries, but they should do that through the shared operations monitor so the monitoring screen can present them safely in a dedicated live panel and warnings section.

**Primary recommendation:** Use SQL read models plus a singleton operations monitor service, then render a shared monitoring dashboard and campaign drill-in via in-place repainting inside the existing console app.

## Codebase Observations

### Existing persisted monitoring data already available
- `ImportBatch` stores started/completed timestamps, status, rows read/imported/rejected.
- `PhoneNumberRecord` stores enrichment timestamps, eligibility, and failure details.
- `Campaign` and `CampaignRecipient` store draft/sending/completed state plus recipient-level status.
- `CampaignRecipientStatusHistory`, `UnmatchedDeliveryCallback`, and `RejectedDeliveryCallback` provide delivery and callback issue history.

### Existing gaps that Phase 5 must close
- `CampaignSendRepository.ListCampaignsAsync` currently returns only `pending`, `submitted`, and `failed`, and sorts by campaign ID rather than recent activity.
- Import, enrichment, campaign send, and callback processing still write directly to `Console.Out`, so there is no shared runtime snapshot for a monitoring screen.
- There is no shared menu entry or workflow that combines import, enrichment, campaign, and delivery state into one operator-facing surface.
- There is no live-job abstraction for the always-on delivery worker or foreground jobs started from menu actions.

## Standard Stack

### Core

| Component | Purpose | Why Standard Here |
|---------|---------|-------------------|
| Existing `Microsoft.Extensions.Hosting` host | Shared runtime composition and singleton service registration | Already established across Phases 1-4 and required for the background delivery worker. |
| EF Core read-only query classes | Monitoring snapshots from SQL Server | Matches the repo’s existing query pattern and keeps monitoring logic separate from write repositories. |
| Singleton operations-monitor service | Track active jobs, recent summaries, warnings, and live delivery entries | Needed because not all operator visibility should come from persisted database state. |
| `PeriodicTimer`-driven render loop | Auto-refresh monitoring screens in place | Smallest standard .NET primitive that fits repainting without introducing a UI framework. |
| Existing xUnit + SQLite test harness | Query and workflow validation | Already used effectively for relational query tests and workflow coverage. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Existing console + repaint loops | `Spectre.Console` or another console UI framework | Richer rendering, but too much surface-area churn for the final phase of a small console app. |
| In-memory operations monitor | Writing all monitoring state only to SQL | Simpler persistence story, but poor fit for live jobs, recent warnings, and the always-on delivery panel. |
| Shared monitoring dashboard | Separate command per subsystem | Easier to implement, but directly conflicts with the user’s choice for one shared view. |

## Architecture Patterns

### Pattern 1: Split Persisted Monitoring From Live Runtime State
**What:** Build monitoring from two sources:
- persisted SQL queries for campaigns, import batches, enrichment failures, and callback issue summaries
- an in-memory singleton for currently running jobs, warnings, and live delivery entries
**When to use:** Everywhere in Phase 5.
**Why:** SQL is the durable source of truth for historical outcomes, but active job state and live lines are transient and should not require extra write tables.

### Pattern 2: Monitoring-Specific Read Models
**What:** Add dedicated query objects and read records instead of reusing send/import workflow DTOs directly.
**When to use:** For campaign summaries, campaign drill-in, callback issue summaries, and recent job summaries.
**Why:** Existing workflow return models do not expose enough fields for monitoring and would couple the screen too tightly to command execution paths.

### Pattern 3: Central Operations Monitor for Job Lifecycle and Warnings
**What:** Add a singleton service that records:
- active jobs by type
- recent completed job summaries
- warning entries ordered newest first
- live delivery log lines for the persistent panel
**When to use:** Instrument import, enrichment, campaign send, and delivery worker paths.
**Why:** The user wants one shared monitoring view with a live panel and disappearing resolved issues, which is a runtime-state concern rather than a pure database concern.

### Pattern 4: In-Place Repaint Rendering
**What:** Render monitoring views through a loop that clears and repaints the current console region instead of appending snapshots.
**When to use:** For the shared monitoring screen and campaign drill-in auto-refresh view.
**Why:** The user explicitly wants auto-refresh with repainting, and continuous append mode would quickly make the console unreadable.

### Pattern 5: Summary List Plus Drill-In
**What:** Keep the main monitoring screen compact, then provide a selected campaign drill-in for recipient-state counts and recent failure/callback detail.
**When to use:** For campaign visibility.
**Why:** This matches the user’s summary-first preference while still exposing detailed failure context.

### Pattern 6: Callback Issues Bucket
**What:** Combine unmatched callbacks and rejected callbacks into one monitoring category while preserving their distinct stored tables underneath.
**When to use:** On the main warnings/issues surface.
**Why:** The user wants one callback-issues bucket, but the delivery pipeline already intentionally stores the two paths separately.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Monitoring from direct scattered `Console.WriteLine` parsing | String-scraping or retroactive console parsing | Explicit operations-monitor events and snapshots | Direct console output is already mixed across workflows and not reliable as a data source. |
| Campaign progress from multiple ad hoc queries in the menu | Several loosely related repository calls | One monitoring query returning the exact dashboard read model | Prevents inconsistent counts and recent-activity ordering bugs. |
| Permanent warning backlog in the main screen | Unbounded active warning list | Newest-first bounded warning snapshot plus drill-in or history queries | The user wants resolved issues to disappear from the main surface. |
| Concurrent background writes while repainting | Raw `Console.Out` from every worker and workflow | Render through a single monitoring surface and shared runtime buffer | Prevents torn output and unreadable screens. |

## Common Pitfalls

- **Reusing `CampaignSendSummaryRecord` for monitoring:** It does not include `queued`, `sent`, `delivered`, or `undelivered`, and it sorts by ID instead of recent activity.
- **Treating direct console output as the monitoring source:** Background delivery output and foreground menu output will collide once repainting screens are introduced.
- **Not defining recent activity centrally:** Campaign ordering will drift unless one query computes the same recent-activity timestamp consistently.
- **Letting live delivery entries grow forever:** The live panel needs a bounded buffer or it will become a memory sink.
- **Making warnings purely historical:** The user wants resolved issues to disappear from the main screen, so the active warnings surface must be distinct from historical drill-in data.
- **Hiding completed campaigns without an explicit affordance:** Operators still need a visible way to request completed campaigns or they will assume campaigns disappeared.
- **Adding a new console framework too late:** This would slow the final phase and force broad changes across menus, tests, and rendering.

## Code Examples

### Monitoring read model split

```csharp
public sealed record MonitoringDashboardSnapshot(
    IReadOnlyList<CampaignMonitorRow> Campaigns,
    IReadOnlyList<JobMonitorRow> ActiveJobs,
    IReadOnlyList<WarningMonitorRow> ActiveWarnings,
    IReadOnlyList<string> LiveDeliveryLines);
```

### Operations monitor lifecycle instrumentation

```csharp
var jobId = operationsMonitor.StartJob(OperationType.Enrichment, "Default due-record enrichment");

try
{
    operationsMonitor.ReportProgress(jobId, processed, total);
    operationsMonitor.CompleteJob(jobId, "Enrichment complete");
}
catch (Exception exception)
{
    operationsMonitor.Warn(OperationType.Enrichment, exception.Message);
    operationsMonitor.FailJob(jobId, exception.Message);
    throw;
}
```

### In-place repaint loop for monitoring

```csharp
using var timer = new PeriodicTimer(refreshInterval);

do
{
    var snapshot = await monitoringWorkflow.GetDashboardAsync(includeCompleted, cancellationToken);
    renderer.Render(snapshot);
}
while (await timer.WaitForNextTickAsync(cancellationToken));
```

## Validation Architecture

- Add query and workflow tests for:
  - campaign counts across all tracked statuses
  - recent-activity ordering
  - completed-campaign hiding unless requested
  - callback-issues aggregation from unmatched plus rejected callback tables
- Add operations-monitor tests for:
  - active job lifecycle transitions
  - newest-first warning ordering
  - resolved issues disappearing from the active warning view
  - bounded live delivery log buffering
- Add menu and rendering tests for:
  - shared monitoring screen composition
  - campaign drill-in rendering
  - explicit completed-campaign reveal behavior
  - repaint-friendly rendering contract
- Add one manual console pass before phase sign-off:
  1. Start the app and open the monitoring screen.
  2. Run an import, enrichment, or campaign send.
  3. Confirm active jobs, warning section, campaign summaries, and live delivery panel update while the screen is open.

## Sources

- Local codebase observations from:
  - `src/PenelopeSMS.App/Menu/*.cs`
  - `src/PenelopeSMS.App/Workflows/*.cs`
  - `src/PenelopeSMS.App/Services/DeliveryCallbackWorker.cs`
  - `src/PenelopeSMS.Domain/Entities/*.cs`
  - `src/PenelopeSMS.Infrastructure/SqlServer/Queries/*.cs`
  - `src/PenelopeSMS.Infrastructure/SqlServer/Repositories/*.cs`
