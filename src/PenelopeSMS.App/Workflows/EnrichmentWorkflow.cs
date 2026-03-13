using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using PenelopeSMS.App.Monitoring;
using PenelopeSMS.Domain.Enums;
using PenelopeSMS.Infrastructure.SqlServer.Queries;
using PenelopeSMS.Infrastructure.SqlServer.Repositories;
using PenelopeSMS.Infrastructure.Twilio;

namespace PenelopeSMS.App.Workflows;

public sealed class EnrichmentWorkflow(
    EnrichmentTargetingQuery enrichmentTargetingQuery,
    PhoneNumberEnrichmentRepository phoneNumberEnrichmentRepository,
    ITwilioLookupClient twilioLookupClient,
    IServiceScopeFactory? serviceScopeFactory = null,
    IOperationsMonitor? runtimeOperationsMonitor = null) : IEnrichmentWorkflow
{
    private const int MaxParallelLookups = 4;
    private readonly IOperationsMonitor operationsMonitor = runtimeOperationsMonitor ?? NullOperationsMonitor.Instance;
    private readonly TextWriter output = Console.Out;

    public EnrichmentWorkflow(
        EnrichmentTargetingQuery enrichmentTargetingQuery,
        PhoneNumberEnrichmentRepository phoneNumberEnrichmentRepository,
        ITwilioLookupClient twilioLookupClient,
        IOperationsMonitor runtimeOperationsMonitor)
        : this(
            enrichmentTargetingQuery,
            phoneNumberEnrichmentRepository,
            twilioLookupClient,
            serviceScopeFactory: null,
            runtimeOperationsMonitor)
    {
    }

    public async Task<EnrichmentWorkflowResult> RunAsync(
        CustomerSegment customerSegment = CustomerSegment.Standard,
        bool fullRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var segmentLabel = customerSegment == CustomerSegment.Vip ? "VIP" : "Standard";
        var label = fullRefresh
            ? $"{segmentLabel} full enrichment refresh"
            : $"{segmentLabel} due-record enrichment";
        var jobId = operationsMonitor.StartJob(OperationType.Enrichment, label, "Selecting records");
        var totalPhoneNumberCount = await enrichmentTargetingQuery.CountImportedPhoneNumbersAsync(customerSegment, cancellationToken);
        var targets = await enrichmentTargetingQuery.ListTargetsAsync(customerSegment, fullRefresh, cancellationToken);

        var processedRecords = 0;
        var updatedRecords = 0;
        var failedRecords = 0;

        if (serviceScopeFactory is null)
        {
            foreach (var target in targets)
            {
                var lookupResult = await twilioLookupClient.LookupAsync(
                    target.CanonicalPhoneNumber,
                    cancellationToken);

                await phoneNumberEnrichmentRepository.ApplyResultAsync(
                    target.PhoneNumberRecordId,
                    lookupResult,
                    cancellationToken);

                processedRecords++;

                if (lookupResult.IsSuccess)
                {
                    updatedRecords++;
                    operationsMonitor.UpdateJob(
                        jobId,
                        $"Processed {processedRecords}/{targets.Count}, Updated {updatedRecords}, Failed {failedRecords}");
                    continue;
                }

                failedRecords++;
                var failureMessage =
                    $"Enrichment failed for {target.CanonicalPhoneNumber}: {lookupResult.ErrorMessage}";
                output.WriteLine(failureMessage);
                operationsMonitor.Warn(OperationType.Enrichment, failureMessage, jobId);
                operationsMonitor.UpdateJob(
                    jobId,
                    $"Processed {processedRecords}/{targets.Count}, Updated {updatedRecords}, Failed {failedRecords}");
            }
        }
        else
        {
            var failureMessages = new ConcurrentQueue<string>();
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxParallelLookups,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(
                targets,
                parallelOptions,
                async (target, token) =>
                {
                    await using var scope = serviceScopeFactory.CreateAsyncScope();
                    var scopedLookupClient = scope.ServiceProvider.GetRequiredService<ITwilioLookupClient>();
                    var scopedRepository = scope.ServiceProvider.GetRequiredService<PhoneNumberEnrichmentRepository>();
                    var lookupResult = await scopedLookupClient.LookupAsync(target.CanonicalPhoneNumber, token);

                    await scopedRepository.ApplyResultAsync(
                        target.PhoneNumberRecordId,
                        lookupResult,
                        token);

                    var processed = Interlocked.Increment(ref processedRecords);

                    if (lookupResult.IsSuccess)
                    {
                        var updated = Interlocked.Increment(ref updatedRecords);
                        operationsMonitor.UpdateJob(
                            jobId,
                            $"Processed {processed}/{targets.Count}, Updated {updated}, Failed {Volatile.Read(ref failedRecords)}");
                        return;
                    }

                    var failed = Interlocked.Increment(ref failedRecords);
                    failureMessages.Enqueue(
                        $"Enrichment failed for {target.CanonicalPhoneNumber}: {lookupResult.ErrorMessage}");
                    operationsMonitor.UpdateJob(
                        jobId,
                        $"Processed {processed}/{targets.Count}, Updated {Volatile.Read(ref updatedRecords)}, Failed {failed}");
                });

            while (failureMessages.TryDequeue(out var failureMessage))
            {
                output.WriteLine(failureMessage);
                operationsMonitor.Warn(OperationType.Enrichment, failureMessage, jobId);
            }
        }

        var skippedRecords = Math.Max(0, totalPhoneNumberCount - targets.Count);
        operationsMonitor.CompleteJob(
            jobId,
            $"Enrichment complete. Selected: {targets.Count}, Processed: {processedRecords}, Updated: {updatedRecords}, Failed: {failedRecords}, Skipped: {skippedRecords}");

        return new EnrichmentWorkflowResult(
            CustomerSegment: customerSegment,
            FullRefresh: fullRefresh,
            SelectedRecords: targets.Count,
            ProcessedRecords: processedRecords,
            UpdatedRecords: updatedRecords,
            FailedRecords: failedRecords,
            SkippedRecords: skippedRecords);
    }
}
