using ShipmentTelemetry.Domain.Enums;
using ShipmentTelemetry.Domain.ValueObjects;

namespace ShipmentTelemetry.Application.Abstractions;

public interface IOperationalStateReadModel
{
    Task UpsertAsync(
        ShipmentId shipmentId,
        ContainerId containerId,
        OperationalMilestone milestone,
        long lastAcceptedSequence,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);

    Task<OperationalStateSnapshot?> GetByShipmentIdAsync(
        ShipmentId shipmentId,
        CancellationToken cancellationToken);
}

public sealed record OperationalStateSnapshot(
    string ShipmentId,
    string ContainerId,
    OperationalMilestone CurrentMilestone,
    long LastAcceptedSequence,
    uint Version,
    DateTimeOffset UpdatedAt);
