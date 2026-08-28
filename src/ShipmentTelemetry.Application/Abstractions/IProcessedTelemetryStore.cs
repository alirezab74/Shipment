using ShipmentTelemetry.Domain.Enums;
using ShipmentTelemetry.Domain.ValueObjects;

namespace ShipmentTelemetry.Application.Abstractions;

public interface IProcessedTelemetryStore
{
    Task<ProcessedTelemetryRecord?> FindByEventIdAsync(
        TelemetryEventId eventId,
        CancellationToken cancellationToken);

    Task<ProcessedTelemetryRecord?> FindByContainerSequenceAsync(
        ContainerId containerId,
        long sequenceNumber,
        CancellationToken cancellationToken);

    Task AddAsync(ProcessedTelemetryRecord record, CancellationToken cancellationToken);
}

public sealed record ProcessedTelemetryRecord(
    TelemetryEventId EventId,
    ContainerId ContainerId,
    ShipmentId ShipmentId,
    long SequenceNumber,
    string PayloadHash,
    TelemetryProcessingOutcome Outcome,
    DateTimeOffset ProcessedAt);
