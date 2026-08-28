namespace ShipmentTelemetry.Contracts.IntegrationEvents;

public sealed record ShipmentMilestoneRecordedIntegrationEvent(
    Guid EventId,
    string ShipmentId,
    string ContainerId,
    string Milestone,
    long SequenceNumber,
    DateTimeOffset RecordedAt);
