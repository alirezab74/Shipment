using ShipmentTelemetry.Domain.Enums;
using ShipmentTelemetry.Domain.Events;

namespace ShipmentTelemetry.Domain.Models;

public sealed record TelemetryProcessingResult(
    TelemetryProcessingOutcome Outcome,
    OperationalMilestone? CurrentMilestone,
    OperationalMilestone? AppliedMilestone,
    ShipmentMilestoneRecordedDomainEvent? MilestoneEvent,
    string? Reason)
{
    public static TelemetryProcessingResult Duplicate(OperationalMilestone? current) =>
        new(TelemetryProcessingOutcome.Duplicate, current, null, null, "Event already processed.");

    public static TelemetryProcessingResult Stale(OperationalMilestone? current, long incomingSequence, long lastAccepted) =>
        new(TelemetryProcessingOutcome.Stale, current, null, null,
            $"Sequence {incomingSequence} is stale; last accepted is {lastAccepted}.");

    public static TelemetryProcessingResult SequenceConflict(string reason) =>
        new(TelemetryProcessingOutcome.SequenceConflict, null, null, null, reason);

    public static TelemetryProcessingResult PayloadConflict() =>
        new(TelemetryProcessingOutcome.PayloadConflict, null, null, null,
            "Same event id with different payload.");

    public static TelemetryProcessingResult InvalidTransition(OperationalMilestone current, OperationalMilestone target) =>
        new(TelemetryProcessingOutcome.InvalidTransition, current, null, null,
            $"Cannot transition from {current} to {target}.");

    public static TelemetryProcessingResult Accepted(
        OperationalMilestone milestone,
        ShipmentMilestoneRecordedDomainEvent domainEvent) =>
        new(TelemetryProcessingOutcome.Accepted, milestone, milestone, domainEvent, null);
}
