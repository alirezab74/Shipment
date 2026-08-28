using ShipmentTelemetry.Domain.Enums;
using ShipmentTelemetry.Domain.ValueObjects;

namespace ShipmentTelemetry.Domain.Events;

public sealed record ShipmentMilestoneRecordedDomainEvent(
    TelemetryEventId EventId,
    ShipmentId ShipmentId,
    ContainerId ContainerId,
    OperationalMilestone Milestone,
    SequenceNumber SequenceNumber,
    DateTimeOffset RecordedAt);
