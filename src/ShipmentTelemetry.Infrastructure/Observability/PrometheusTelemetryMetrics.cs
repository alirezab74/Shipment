using Prometheus;
using ShipmentTelemetry.Application.Abstractions;
using ShipmentTelemetry.Domain.Enums;

namespace ShipmentTelemetry.Infrastructure.Observability;

public sealed class PrometheusTelemetryMetrics : ITelemetryMetrics
{
    private static readonly Counter TelemetryReceivedTotal = Metrics.CreateCounter(
        "telemetry_received_total",
        "Total telemetry events received.");

    private static readonly Counter TelemetryDuplicateTotal = Metrics.CreateCounter(
        "telemetry_duplicate_total",
        "Total duplicate telemetry events.");

    private static readonly Counter TelemetryStaleTotal = Metrics.CreateCounter(
        "telemetry_stale_total",
        "Total stale telemetry events.");

    private static readonly Counter MilestoneRecordedTotal = Metrics.CreateCounter(
        "milestone_recorded_total",
        "Total shipment milestones recorded.");

    private static readonly Counter MilestoneRejectedTotal = Metrics.CreateCounter(
        "milestone_rejected_total",
        "Total rejected milestone transitions.",
        "outcome");

    private static readonly Counter ConcurrencyConflictTotal = Metrics.CreateCounter(
        "telemetry_concurrency_conflict_total",
        "Total optimistic concurrency conflicts during telemetry processing.");

    private static readonly Counter OutboxPublishedTotal = Metrics.CreateCounter(
        "outbox_published_total",
        "Total outbox messages published.");

    private static readonly Gauge OutboxBacklog = Metrics.CreateGauge(
        "outbox_backlog",
        "Current count of pending outbox messages.");

    private static readonly Histogram ProcessingDurationHistogram = Metrics.CreateHistogram(
        "telemetry_processing_duration_seconds",
        "Telemetry processing duration in seconds.",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 12)
        });

    public void TelemetryReceived() => TelemetryReceivedTotal.Inc();

    public void TelemetryDuplicate() => TelemetryDuplicateTotal.Inc();

    public void TelemetryStale() => TelemetryStaleTotal.Inc();

    public void MilestoneRecorded() => MilestoneRecordedTotal.Inc();

    public void MilestoneRejected(TelemetryProcessingOutcome outcome) =>
        MilestoneRejectedTotal.WithLabels(outcome.ToString()).Inc();

    public void ConcurrencyConflict() => ConcurrencyConflictTotal.Inc();

    public void OutboxPublished() => OutboxPublishedTotal.Inc();

    public void ProcessingDuration(TimeSpan duration) =>
        ProcessingDurationHistogram.Observe(duration.TotalSeconds);

    public static void SetOutboxBacklog(long count) => OutboxBacklog.Set(count);
}
