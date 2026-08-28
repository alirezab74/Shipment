using ShipmentTelemetry.Domain.Enums;
using ShipmentTelemetry.Domain.ValueObjects;

namespace ShipmentTelemetry.Application.Abstractions;

public interface ITelemetryStatusStore
{
    Task SaveAsync(TelemetryStatusRecord record, CancellationToken cancellationToken);

    Task<TelemetryStatusRecord?> GetByEventIdAsync(
        TelemetryEventId eventId,
        CancellationToken cancellationToken);
}

public sealed record TelemetryStatusRecord(
    TelemetryEventId EventId,
    TelemetryProcessingOutcome Outcome,
    string? Reason,
    OperationalMilestone? CurrentMilestone,
    DateTimeOffset ProcessedAt);
