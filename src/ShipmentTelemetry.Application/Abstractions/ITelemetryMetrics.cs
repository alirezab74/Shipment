using ShipmentTelemetry.Domain.Enums;

namespace ShipmentTelemetry.Application.Abstractions;

public interface ITelemetryMetrics
{
    void TelemetryReceived();
    void TelemetryDuplicate();
    void TelemetryStale();
    void MilestoneRecorded();
    void MilestoneRejected(TelemetryProcessingOutcome outcome);
    void ConcurrencyConflict();
    void OutboxPublished();
    void ProcessingDuration(TimeSpan duration);
}
