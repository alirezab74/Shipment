using ShipmentTelemetry.Domain.Common;
using ShipmentTelemetry.Domain.Enums;
using ShipmentTelemetry.Domain.Events;
using ShipmentTelemetry.Domain.Models;
using ShipmentTelemetry.Domain.ValueObjects;

namespace ShipmentTelemetry.Domain.Aggregates;

public sealed class ShipmentOperationalState : AggregateRoot
{
    private ShipmentOperationalState(
        ShipmentId shipmentId,
        ContainerId containerId,
        OperationalMilestone currentMilestone,
        long lastAcceptedSequence)
    {
        ShipmentId = shipmentId;
        ContainerId = containerId;
        CurrentMilestone = currentMilestone;
        LastAcceptedSequence = lastAcceptedSequence;
    }

    public ShipmentId ShipmentId { get; private set; } = null!;

    public ContainerId ContainerId { get; private set; } = null!;

    public OperationalMilestone CurrentMilestone { get; private set; }

    public long LastAcceptedSequence { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static ShipmentOperationalState Create(ShipmentId shipmentId, ContainerId containerId) =>
        new(shipmentId, containerId, OperationalMilestone.None, -1);

    public TelemetryProcessingResult ProcessTelemetry(TelemetryEnvelope envelope)
    {
        if (!ShipmentId.Value.Equals(envelope.ShipmentId.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Telemetry shipment id does not match aggregate.");
        }

        if (!ContainerId.Value.Equals(envelope.ContainerId.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Telemetry container id does not match aggregate.");
        }

        if (envelope.SequenceNumber.IsStaleComparedTo(LastAcceptedSequence))
        {
            return TelemetryProcessingResult.Stale(
                CurrentMilestone,
                envelope.SequenceNumber.Value,
                LastAcceptedSequence);
        }

        var targetMilestone = MapToMilestone(envelope.EventType);

        if (!MilestoneTransitionRules.CanTransition(CurrentMilestone, targetMilestone))
        {
            return TelemetryProcessingResult.InvalidTransition(CurrentMilestone, targetMilestone);
        }

        if (targetMilestone <= CurrentMilestone && CurrentMilestone != OperationalMilestone.None)
        {
            return TelemetryProcessingResult.InvalidTransition(CurrentMilestone, targetMilestone);
        }

        CurrentMilestone = targetMilestone;
        LastAcceptedSequence = envelope.SequenceNumber.Value;
        UpdatedAt = DateTimeOffset.UtcNow;
        Version++;

        var domainEvent = new ShipmentMilestoneRecordedDomainEvent(
            envelope.EventId,
            ShipmentId,
            ContainerId,
            targetMilestone,
            envelope.SequenceNumber,
            UpdatedAt);

        RaiseDomainEvent(domainEvent);

        return TelemetryProcessingResult.Accepted(targetMilestone, domainEvent);
    }

    private static OperationalMilestone MapToMilestone(TelemetryEventType eventType) =>
        eventType switch
        {
            TelemetryEventType.LocationReported => OperationalMilestone.ArrivedAtPort,
            TelemetryEventType.GateInDetected => OperationalMilestone.GateIn,
            TelemetryEventType.LoadConfirmed => OperationalMilestone.LoadedOnVessel,
            TelemetryEventType.VesselDepartureDetected => OperationalMilestone.DepartedPort,
            TelemetryEventType.GateOutDetected => OperationalMilestone.GateOut,
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unknown telemetry event type.")
        };

    public static ShipmentOperationalState Restore(
        ShipmentId shipmentId,
        ContainerId containerId,
        OperationalMilestone currentMilestone,
        long lastAcceptedSequence,
        uint version,
        DateTimeOffset updatedAt)
    {
        var state = new ShipmentOperationalState(shipmentId, containerId, currentMilestone, lastAcceptedSequence)
        {
            Version = version,
            UpdatedAt = updatedAt
        };

        return state;
    }
}
