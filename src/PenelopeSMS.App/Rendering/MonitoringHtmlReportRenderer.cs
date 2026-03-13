using System.Globalization;
using System.Net;
using System.Text;
using PenelopeSMS.App.Workflows;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.SqlServer.Queries;

namespace PenelopeSMS.App.Rendering;

public sealed class MonitoringHtmlReportRenderer
{
    public string Render(MonitoringHtmlReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new StringBuilder();
        var overview = document.ReportData.Overview;
        var campaigns = document.Dashboard.Campaigns;
        var issues = document.Dashboard.PersistedIssues;

        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\">");
        builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine("  <title>PenelopeSMS Monitoring Report</title>");
        builder.AppendLine("  <style>");
        builder.AppendLine("""
            :root {
                --bg: #f4efe6;
                --panel: rgba(255, 252, 247, 0.9);
                --panel-strong: #fffaf2;
                --text: #1f2933;
                --muted: #5d6b77;
                --accent: #0f766e;
                --accent-soft: #d5efeb;
                --warn: #b45309;
                --warn-soft: #fdf0d5;
                --danger: #b42318;
                --danger-soft: #fce8e5;
                --border: rgba(31, 41, 51, 0.12);
                --shadow: 0 24px 60px rgba(71, 85, 105, 0.14);
            }

            * {
                box-sizing: border-box;
            }

            body {
                margin: 0;
                font-family: "Segoe UI", Tahoma, Geneva, Verdana, sans-serif;
                color: var(--text);
                background:
                    radial-gradient(circle at top left, rgba(15, 118, 110, 0.14), transparent 28%),
                    radial-gradient(circle at top right, rgba(180, 83, 9, 0.12), transparent 22%),
                    linear-gradient(180deg, #fcfaf6 0%, var(--bg) 100%);
            }

            h1, h2, h3 {
                margin: 0;
                font-family: Georgia, "Times New Roman", serif;
                letter-spacing: 0.01em;
            }

            p {
                margin: 0;
            }

            .page {
                max-width: 1360px;
                margin: 0 auto;
                padding: 32px 24px 48px;
            }

            .hero {
                display: grid;
                grid-template-columns: 2fr 1fr;
                gap: 20px;
                margin-bottom: 24px;
            }

            .panel {
                background: var(--panel);
                border: 1px solid var(--border);
                border-radius: 24px;
                box-shadow: var(--shadow);
                backdrop-filter: blur(10px);
            }

            .panel-strong {
                background: var(--panel-strong);
                border: 1px solid rgba(31, 41, 51, 0.08);
                border-radius: 20px;
            }

            .hero-main {
                padding: 28px;
                background:
                    linear-gradient(135deg, rgba(15, 118, 110, 0.94), rgba(17, 94, 89, 0.88)),
                    linear-gradient(180deg, #0f766e 0%, #134e4a 100%);
                color: #f8fffe;
                overflow: hidden;
                position: relative;
            }

            .hero-main::after {
                content: "";
                position: absolute;
                inset: auto -60px -80px auto;
                width: 220px;
                height: 220px;
                border-radius: 999px;
                background: rgba(255, 255, 255, 0.12);
            }

            .eyebrow {
                display: inline-flex;
                align-items: center;
                gap: 8px;
                font-size: 0.85rem;
                letter-spacing: 0.08em;
                text-transform: uppercase;
                opacity: 0.84;
                margin-bottom: 14px;
            }

            .hero-main h1 {
                font-size: clamp(2rem, 3vw, 3rem);
                margin-bottom: 12px;
            }

            .hero-copy {
                max-width: 56ch;
                line-height: 1.55;
                color: rgba(248, 255, 254, 0.9);
            }

            .hero-meta {
                margin-top: 18px;
                display: flex;
                flex-wrap: wrap;
                gap: 10px;
            }

            .pill {
                display: inline-flex;
                align-items: center;
                gap: 6px;
                padding: 8px 12px;
                border-radius: 999px;
                font-size: 0.85rem;
                font-weight: 600;
                background: rgba(255, 255, 255, 0.12);
                color: inherit;
            }

            .hero-side {
                padding: 24px;
                display: grid;
                align-content: start;
                gap: 16px;
                background:
                    linear-gradient(180deg, rgba(255, 250, 242, 0.96), rgba(250, 242, 231, 0.96));
            }

            .section {
                margin-top: 24px;
            }

            .section-title {
                margin-bottom: 14px;
                display: flex;
                justify-content: space-between;
                align-items: baseline;
                gap: 16px;
            }

            .section-title p {
                color: var(--muted);
            }

            .card-grid {
                display: grid;
                grid-template-columns: repeat(6, minmax(0, 1fr));
                gap: 16px;
            }

            .metric-card {
                padding: 20px;
            }

            .metric-label {
                color: var(--muted);
                font-size: 0.92rem;
                margin-bottom: 8px;
            }

            .metric-value {
                font-size: 2rem;
                font-weight: 700;
                margin-bottom: 6px;
            }

            .metric-note {
                font-size: 0.92rem;
                color: var(--muted);
            }

            .content-grid {
                display: grid;
                grid-template-columns: 1.2fr 0.8fr;
                gap: 16px;
            }

            .line-type-list,
            .issue-list,
            .failure-code-list,
            .job-list {
                display: grid;
                gap: 12px;
            }

            .stack-card {
                padding: 18px 20px;
            }

            .stack-top {
                display: flex;
                justify-content: space-between;
                gap: 12px;
                margin-bottom: 10px;
                align-items: center;
            }

            .stack-top strong {
                font-size: 1rem;
            }

            .stack-meta {
                color: var(--muted);
                font-size: 0.9rem;
                display: flex;
                flex-wrap: wrap;
                gap: 12px;
            }

            .meter {
                position: relative;
                height: 10px;
                border-radius: 999px;
                background: #e6ecef;
                overflow: hidden;
                margin-top: 12px;
            }

            .meter-fill {
                position: absolute;
                inset: 0 auto 0 0;
                border-radius: inherit;
                background: linear-gradient(90deg, #0f766e, #14b8a6);
            }

            .meter-fill.warn {
                background: linear-gradient(90deg, #b45309, #f59e0b);
            }

            .table-panel {
                overflow: hidden;
            }

            table {
                width: 100%;
                border-collapse: collapse;
            }

            th, td {
                padding: 14px 16px;
                text-align: left;
                border-bottom: 1px solid rgba(31, 41, 51, 0.08);
                vertical-align: top;
            }

            th {
                color: var(--muted);
                font-size: 0.84rem;
                text-transform: uppercase;
                letter-spacing: 0.06em;
                background: rgba(255, 255, 255, 0.55);
            }

            tr.total-line td {
                background: rgba(15, 118, 110, 0.06);
                font-weight: 600;
            }

            tr.total-source td {
                background: rgba(180, 83, 9, 0.08);
                font-weight: 700;
            }

            tr.total-grand td {
                background: rgba(31, 41, 51, 0.08);
                font-weight: 800;
            }

            .tag {
                display: inline-flex;
                align-items: center;
                padding: 4px 10px;
                border-radius: 999px;
                font-size: 0.78rem;
                font-weight: 700;
                letter-spacing: 0.03em;
            }

            .tag.detail {
                background: rgba(15, 118, 110, 0.08);
                color: #0f766e;
            }

            .tag.line {
                background: rgba(20, 184, 166, 0.12);
                color: #0f766e;
            }

            .tag.source {
                background: rgba(180, 83, 9, 0.12);
                color: #92400e;
            }

            .tag.grand {
                background: rgba(31, 41, 51, 0.12);
                color: #1f2933;
            }

            .status-flag {
                display: inline-flex;
                padding: 6px 10px;
                border-radius: 999px;
                font-size: 0.78rem;
                font-weight: 700;
            }

            .status-flag.retryable {
                background: var(--warn-soft);
                color: var(--warn);
            }

            .status-flag.permanent,
            .status-flag.failed {
                background: var(--danger-soft);
                color: var(--danger);
            }

            .status-flag.good,
            .status-flag.completed {
                background: var(--accent-soft);
                color: var(--accent);
            }

            .small {
                color: var(--muted);
                font-size: 0.9rem;
            }

            .empty {
                padding: 20px;
                color: var(--muted);
            }

            @media (max-width: 1100px) {
                .hero,
                .content-grid {
                    grid-template-columns: 1fr;
                }

                .card-grid {
                    grid-template-columns: repeat(3, minmax(0, 1fr));
                }
            }

            @media (max-width: 720px) {
                .page {
                    padding: 18px 14px 32px;
                }

                .card-grid {
                    grid-template-columns: repeat(2, minmax(0, 1fr));
                }

                th, td {
                    padding: 12px 10px;
                }
            }
            """);
        builder.AppendLine("  </style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("  <main class=\"page\">");
        builder.AppendLine("    <section class=\"hero\">");
        builder.AppendLine("      <article class=\"panel hero-main\">");
        builder.AppendLine("        <div class=\"eyebrow\">PenelopeSMS monitoring export</div>");
        builder.AppendLine("        <h1>Carrier footprint and enrichment health</h1>");
        builder.AppendLine("        <p class=\"hero-copy\">This report combines Twilio line-type coverage, imported phone source mix, enrichment failures, campaign readiness, and current operational signals into a single exportable HTML snapshot.</p>");
        builder.AppendLine("        <div class=\"hero-meta\">");
        AppendInvariantLine(builder, $"          <span class=\"pill\">Generated {Encode(FormatUtc(document.GeneratedAtUtc))}</span>");
        AppendInvariantLine(builder, $"          <span class=\"pill\">{overview.TotalPhoneNumbers.ToString("N0", CultureInfo.InvariantCulture)} phone records</span>");
        AppendInvariantLine(builder, $"          <span class=\"pill\">{campaigns.Count.ToString("N0", CultureInfo.InvariantCulture)} campaigns in scope</span>");
        builder.AppendLine("        </div>");
        builder.AppendLine("      </article>");
        builder.AppendLine("      <aside class=\"panel hero-side\">");
        builder.AppendLine("        <h3>Health snapshot</h3>");
        AppendInvariantLine(builder, $"        <p class=\"small\">Successful enrichment rate: {Percent(overview.SuccessfulEnrichmentCount, overview.TotalPhoneNumbers)}</p>");
        AppendInvariantLine(builder, $"        <p class=\"small\">Failed enrichment rate: {Percent(overview.FailedEnrichmentCount, overview.TotalPhoneNumbers)}</p>");
        AppendInvariantLine(builder, $"        <p class=\"small\">Campaign eligible rate: {Percent(overview.EligibleForCampaignsCount, overview.TotalPhoneNumbers)}</p>");
        AppendInvariantLine(builder, $"        <p class=\"small\">Active persisted issues: {issues.Count.ToString("N0", CultureInfo.InvariantCulture)}</p>");
        builder.AppendLine("      </aside>");
        builder.AppendLine("    </section>");

        builder.AppendLine("    <section class=\"section\">");
        builder.AppendLine("      <div class=\"section-title\">");
        builder.AppendLine("        <div><h2>Overview</h2><p>Current enrichment and campaign-readiness totals.</p></div>");
        builder.AppendLine("      </div>");
        builder.AppendLine("      <div class=\"card-grid\">");
        AppendMetricCard(builder, "Total phone records", overview.TotalPhoneNumbers, "Imported canonical phone numbers on file.");
        AppendMetricCard(builder, "Successful enrichment", overview.SuccessfulEnrichmentCount, "Records with a successful Twilio enrichment snapshot.");
        AppendMetricCard(builder, "Failed enrichment", overview.FailedEnrichmentCount, "Records still carrying retryable or permanent enrichment failures.");
        AppendMetricCard(builder, "Pending enrichment", overview.PendingEnrichmentCount, "Records that have not succeeded and do not currently carry a failure flag.");
        AppendMetricCard(builder, "Campaign eligible", overview.EligibleForCampaignsCount, "Ready to target in campaigns based on current enrichment.");
        AppendMetricCard(builder, "Campaign ineligible", overview.IneligibleForCampaignsCount, "Flagged as not sendable after enrichment.");
        builder.AppendLine("      </div>");
        builder.AppendLine("    </section>");

        builder.AppendLine("    <section class=\"section content-grid\">");
        builder.AppendLine("      <article class=\"panel stack-card\">");
        builder.AppendLine("        <div class=\"section-title\">");
        builder.AppendLine("          <div><h2>Line Type Mix</h2><p>Successful enrichments grouped by Twilio line type.</p></div>");
        builder.AppendLine("        </div>");
        AppendLineTypeCards(builder, document.ReportData.LineTypeSummaries, overview.SuccessfulEnrichmentCount);
        builder.AppendLine("      </article>");

        builder.AppendLine("      <article class=\"panel stack-card\">");
        builder.AppendLine("        <div class=\"section-title\">");
        builder.AppendLine("          <div><h2>Operational Issues</h2><p>Persisted warnings from callbacks, imports, and failed enrichment.</p></div>");
        builder.AppendLine("        </div>");
        AppendIssueList(builder, issues);
        builder.AppendLine("      </article>");
        builder.AppendLine("    </section>");

        builder.AppendLine("    <section class=\"section\">");
        builder.AppendLine("      <div class=\"section-title\">");
        builder.AppendLine("        <div><h2>Carrier Footprint</h2><p>Rollup of imported phone source, Twilio line type, and carrier counts aligned with the enrichment carrier query.</p></div>");
        builder.AppendLine("      </div>");
        builder.AppendLine("      <div class=\"panel table-panel\">");
        AppendCarrierRollupTable(builder, document.ReportData.CarrierRollups);
        builder.AppendLine("      </div>");
        builder.AppendLine("    </section>");

        builder.AppendLine("    <section class=\"section content-grid\">");
        builder.AppendLine("      <article class=\"panel stack-card\">");
        builder.AppendLine("        <div class=\"section-title\">");
        builder.AppendLine("          <div><h2>Failure Breakdown</h2><p>Retryable and permanent enrichment problems, including common provider error codes.</p></div>");
        builder.AppendLine("        </div>");
        AppendFailureSummary(builder, document.ReportData.FailureStatusSummaries, document.ReportData.FailureCodeSummaries);
        builder.AppendLine("      </article>");

        builder.AppendLine("      <article class=\"panel stack-card\">");
        builder.AppendLine("        <div class=\"section-title\">");
        builder.AppendLine("          <div><h2>Campaign Snapshot</h2><p>Current campaign execution state, including completed campaigns.</p></div>");
        builder.AppendLine("        </div>");
        AppendCampaignList(builder, campaigns);
        builder.AppendLine("      </article>");
        builder.AppendLine("    </section>");

        builder.AppendLine("    <section class=\"section\">");
        builder.AppendLine("      <div class=\"section-title\">");
        builder.AppendLine("        <div><h2>Recent Enrichment Failures</h2><p>Most recent failed enrichment records with eligibility context.</p></div>");
        builder.AppendLine("      </div>");
        builder.AppendLine("      <div class=\"panel table-panel\">");
        AppendRecentFailures(builder, document.ReportData.RecentFailures);
        builder.AppendLine("      </div>");
        builder.AppendLine("    </section>");

        builder.AppendLine("    <section class=\"section\">");
        builder.AppendLine("      <div class=\"section-title\">");
        builder.AppendLine("        <div><h2>Recent Completed Jobs</h2><p>Latest persisted and live-session job summaries from monitoring.</p></div>");
        builder.AppendLine("      </div>");
        builder.AppendLine("      <article class=\"panel stack-card\">");
        AppendCompletedJobs(builder, document.Dashboard.CompletedJobs);
        builder.AppendLine("      </article>");
        builder.AppendLine("    </section>");

        builder.AppendLine("  </main>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static void AppendMetricCard(StringBuilder builder, string label, int value, string note)
    {
        builder.AppendLine("        <article class=\"panel metric-card\">");
        AppendInvariantLine(builder, $"          <div class=\"metric-label\">{Encode(label)}</div>");
        AppendInvariantLine(builder, $"          <div class=\"metric-value\">{value.ToString("N0", CultureInfo.InvariantCulture)}</div>");
        AppendInvariantLine(builder, $"          <div class=\"metric-note\">{Encode(note)}</div>");
        builder.AppendLine("        </article>");
    }

    private static void AppendLineTypeCards(
        StringBuilder builder,
        IReadOnlyList<MonitoringLineTypeSummaryRecord> lineTypes,
        int successfulEnrichmentCount)
    {
        if (lineTypes.Count == 0)
        {
            builder.AppendLine("        <div class=\"empty\">No successful enrichment records with Twilio line types were found.</div>");
            return;
        }

        builder.AppendLine("        <div class=\"line-type-list\">");

        foreach (var lineType in lineTypes)
        {
            var width = PercentWidth(lineType.RecordCount, successfulEnrichmentCount);
            builder.AppendLine("          <div class=\"stack-card panel-strong\">");
            builder.AppendLine("            <div class=\"stack-top\">");
            AppendInvariantLine(builder, $"              <strong>{Encode(lineType.LineType)}</strong>");
            AppendInvariantLine(builder, $"              <span>{lineType.RecordCount.ToString("N0", CultureInfo.InvariantCulture)}</span>");
            builder.AppendLine("            </div>");
            AppendInvariantLine(builder, $"            <div class=\"stack-meta\"><span>Eligible: {lineType.EligibleCount}</span><span>Ineligible: {lineType.IneligibleCount}</span><span>Pending: {lineType.PendingCount}</span></div>");
            AppendInvariantLine(builder, $"            <div class=\"meter\"><span class=\"meter-fill\" style=\"width: {width.ToString("0.##", CultureInfo.InvariantCulture)}%\"></span></div>");
            builder.AppendLine("          </div>");
        }

        builder.AppendLine("        </div>");
    }

    private static void AppendIssueList(
        StringBuilder builder,
        IReadOnlyList<PersistedMonitoringIssueRecord> issues)
    {
        if (issues.Count == 0)
        {
            builder.AppendLine("        <div class=\"empty\">No persisted monitoring issues are currently on record.</div>");
            return;
        }

        builder.AppendLine("        <div class=\"issue-list\">");

        foreach (var issue in issues.OrderByDescending(issue => issue.LastOccurredAtUtc))
        {
            builder.AppendLine("          <div class=\"stack-card panel-strong\">");
            builder.AppendLine("            <div class=\"stack-top\">");
            AppendInvariantLine(builder, $"              <strong>{Encode(issue.Label)}</strong>");
            AppendInvariantLine(builder, $"              <span>{issue.Count.ToString("N0", CultureInfo.InvariantCulture)}</span>");
            builder.AppendLine("            </div>");
            AppendInvariantLine(builder, $"            <p class=\"small\">{Encode(issue.Detail)}</p>");
            AppendInvariantLine(builder, $"            <p class=\"small\" style=\"margin-top:10px;\">Last seen: {Encode(FormatUtc(issue.LastOccurredAtUtc))}</p>");
            builder.AppendLine("          </div>");
        }

        builder.AppendLine("        </div>");
    }

    private static void AppendCarrierRollupTable(
        StringBuilder builder,
        IReadOnlyList<MonitoringCarrierRollupRecord> rows)
    {
        builder.AppendLine("        <table>");
        builder.AppendLine("          <thead><tr><th>Level</th><th>Imported Source</th><th>Line Type</th><th>Carrier</th><th>Count</th></tr></thead>");
        builder.AppendLine("          <tbody>");

        foreach (var row in rows)
        {
            var rowClass = row.RollupLevel switch
            {
                MonitoringCarrierRollupLevel.LineTypeTotal => "total-line",
                MonitoringCarrierRollupLevel.SourceTotal => "total-source",
                MonitoringCarrierRollupLevel.GrandTotal => "total-grand",
                _ => string.Empty
            };

            AppendInvariantLine(builder, $"            <tr class=\"{rowClass}\">");
            AppendInvariantLine(builder, $"              <td>{RenderRollupTag(row.RollupLevel)}</td>");
            AppendInvariantLine(builder, $"              <td>{Encode(row.SourceLabel)}</td>");
            AppendInvariantLine(builder, $"              <td>{Encode(row.LineTypeLabel)}</td>");
            AppendInvariantLine(builder, $"              <td>{Encode(row.CarrierLabel)}</td>");
            AppendInvariantLine(builder, $"              <td>{row.RecordCount.ToString("N0", CultureInfo.InvariantCulture)}</td>");
            builder.AppendLine("            </tr>");
        }

        builder.AppendLine("          </tbody>");
        builder.AppendLine("        </table>");
    }

    private static void AppendFailureSummary(
        StringBuilder builder,
        IReadOnlyList<MonitoringFailureStatusSummaryRecord> statusRows,
        IReadOnlyList<MonitoringFailureCodeSummaryRecord> codeRows)
    {
        if (statusRows.Count == 0)
        {
            builder.AppendLine("        <div class=\"empty\">No failed enrichment rows are currently on record.</div>");
            return;
        }

        builder.AppendLine("        <div class=\"line-type-list\">");

        foreach (var statusRow in statusRows)
        {
            builder.AppendLine("          <div class=\"stack-card panel-strong\">");
            builder.AppendLine("            <div class=\"stack-top\">");
            AppendInvariantLine(builder, $"              <span class=\"status-flag {FailureTone(statusRow.FailureStatus)}\">{Encode(statusRow.FailureStatus.ToString())}</span>");
            AppendInvariantLine(builder, $"              <span>{statusRow.RecordCount.ToString("N0", CultureInfo.InvariantCulture)}</span>");
            builder.AppendLine("            </div>");
            AppendInvariantLine(builder, $"            <p class=\"small\">Last failure: {Encode(FormatUtc(statusRow.LastFailedAtUtc))}</p>");
            builder.AppendLine("          </div>");
        }

        builder.AppendLine("        </div>");
        builder.AppendLine("        <div style=\"height: 18px\"></div>");
        builder.AppendLine("        <div class=\"failure-code-list\">");

        foreach (var codeRow in codeRows)
        {
            builder.AppendLine("          <div class=\"stack-card panel-strong\">");
            builder.AppendLine("            <div class=\"stack-top\">");
            AppendInvariantLine(builder, $"              <strong>{Encode(codeRow.ErrorCode)}</strong>");
            AppendInvariantLine(builder, $"              <span>{codeRow.RecordCount.ToString("N0", CultureInfo.InvariantCulture)}</span>");
            builder.AppendLine("            </div>");
            AppendInvariantLine(builder, $"            <p class=\"small\">{Encode(codeRow.FailureStatus.ToString())} | {Encode(codeRow.ErrorMessage)}</p>");
            builder.AppendLine("          </div>");
        }

        builder.AppendLine("        </div>");
    }

    private static void AppendCampaignList(
        StringBuilder builder,
        IReadOnlyList<CampaignMonitoringSummaryRecord> campaigns)
    {
        if (campaigns.Count == 0)
        {
            builder.AppendLine("        <div class=\"empty\">No campaigns are present in the monitoring snapshot.</div>");
            return;
        }

        builder.AppendLine("        <div class=\"job-list\">");

        foreach (var campaign in campaigns.Take(12))
        {
            builder.AppendLine("          <div class=\"stack-card panel-strong\">");
            builder.AppendLine("            <div class=\"stack-top\">");
            AppendInvariantLine(builder, $"              <strong>{Encode(campaign.CampaignName)}</strong>");
            AppendInvariantLine(builder, $"              <span class=\"status-flag {CampaignTone(campaign.Status)}\">{Encode(campaign.Status.ToString())}</span>");
            builder.AppendLine("            </div>");
            AppendInvariantLine(builder, $"            <div class=\"stack-meta\"><span>Batch size: {campaign.BatchSize}</span><span>Pending: {campaign.PendingRecipients}</span><span>Delivered: {campaign.DeliveredRecipients}</span><span>Failed: {campaign.FailedRecipients}</span></div>");
            AppendInvariantLine(builder, $"            <p class=\"small\" style=\"margin-top:10px;\">Last activity: {Encode(FormatUtc(campaign.LastActivityAtUtc))}</p>");
            builder.AppendLine("          </div>");
        }

        builder.AppendLine("        </div>");
    }

    private static void AppendRecentFailures(
        StringBuilder builder,
        IReadOnlyList<MonitoringRecentFailureRecord> failures)
    {
        builder.AppendLine("        <table>");
        builder.AppendLine("          <thead><tr><th>Record</th><th>Phone Number</th><th>Status</th><th>Eligibility</th><th>Occurred</th><th>Error</th></tr></thead>");
        builder.AppendLine("          <tbody>");

        if (failures.Count == 0)
        {
            builder.AppendLine("            <tr><td colspan=\"6\" class=\"empty\">No recent enrichment failures found.</td></tr>");
        }
        else
        {
            foreach (var failure in failures)
            {
                builder.AppendLine("            <tr>");
                AppendInvariantLine(builder, $"              <td>{failure.PhoneNumberRecordId.ToString(CultureInfo.InvariantCulture)}</td>");
                AppendInvariantLine(builder, $"              <td>{Encode(failure.CanonicalPhoneNumber)}</td>");
                AppendInvariantLine(builder, $"              <td><span class=\"status-flag {FailureTone(failure.FailureStatus)}\">{Encode(failure.FailureStatus.ToString())}</span></td>");
                AppendInvariantLine(builder, $"              <td>{Encode(failure.EligibilityStatus.ToString())}</td>");
                AppendInvariantLine(builder, $"              <td>{Encode(FormatUtc(failure.LastFailedAtUtc))}</td>");
                AppendInvariantLine(builder, $"              <td>{Encode($"{failure.ErrorCode ?? "no-code"} | {failure.ErrorMessage ?? "No provider detail"}")}</td>");
                builder.AppendLine("            </tr>");
            }
        }

        builder.AppendLine("          </tbody>");
        builder.AppendLine("        </table>");
    }

    private static void AppendCompletedJobs(
        StringBuilder builder,
        IReadOnlyList<MonitoringCompletedJobRecord> completedJobs)
    {
        if (completedJobs.Count == 0)
        {
            builder.AppendLine("        <div class=\"empty\">No completed jobs are available in the monitoring snapshot.</div>");
            return;
        }

        builder.AppendLine("        <div class=\"job-list\">");

        foreach (var job in completedJobs)
        {
            builder.AppendLine("          <div class=\"stack-card panel-strong\">");
            builder.AppendLine("            <div class=\"stack-top\">");
            AppendInvariantLine(builder, $"              <strong>{Encode(job.Label)}</strong>");
            AppendInvariantLine(builder, $"              <span class=\"status-flag {JobTone(job.Outcome)}\">{Encode(job.Outcome)}</span>");
            builder.AppendLine("            </div>");
            AppendInvariantLine(builder, $"            <p class=\"small\">{Encode(job.Summary)}</p>");
            AppendInvariantLine(builder, $"            <p class=\"small\" style=\"margin-top:10px;\">{Encode(FormatUtc(job.CompletedAtUtc))} | {(job.IsLiveSession ? "Live session" : "Persisted history")}</p>");
            builder.AppendLine("          </div>");
        }

        builder.AppendLine("        </div>");
    }

    private static string RenderRollupTag(MonitoringCarrierRollupLevel level)
    {
        return level switch
        {
            MonitoringCarrierRollupLevel.LineTypeTotal => "<span class=\"tag line\">Line total</span>",
            MonitoringCarrierRollupLevel.SourceTotal => "<span class=\"tag source\">Source total</span>",
            MonitoringCarrierRollupLevel.GrandTotal => "<span class=\"tag grand\">Grand total</span>",
            _ => "<span class=\"tag detail\">Detail</span>"
        };
    }

    private static string FailureTone(EnrichmentFailureStatus failureStatus)
    {
        return failureStatus switch
        {
            EnrichmentFailureStatus.Retryable => "retryable",
            EnrichmentFailureStatus.Permanent => "permanent",
            _ => "failed"
        };
    }

    private static string CampaignTone(CampaignStatus campaignStatus)
    {
        return campaignStatus == CampaignStatus.Completed ? "completed" : "good";
    }

    private static string JobTone(string outcome)
    {
        return outcome.Contains("fail", StringComparison.OrdinalIgnoreCase)
            ? "failed"
            : "completed";
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static string FormatUtc(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)
            : "n/a";
    }

    private static void AppendInvariantLine(StringBuilder builder, FormattableString value)
    {
        builder.AppendLine(value.ToString(CultureInfo.InvariantCulture));
    }

    private static string Percent(int count, int total)
    {
        return total <= 0
            ? "0.0%"
            : ((double)count / total).ToString("P1", CultureInfo.InvariantCulture);
    }

    private static double PercentWidth(int count, int total)
    {
        if (count <= 0 || total <= 0)
        {
            return 0d;
        }

        return Math.Max(6d, Math.Round((double)count / total * 100d, 2));
    }
}

public sealed record MonitoringHtmlReportDocument(
    DateTime GeneratedAtUtc,
    MonitoringDashboardSnapshot Dashboard,
    MonitoringHtmlReportData ReportData);
