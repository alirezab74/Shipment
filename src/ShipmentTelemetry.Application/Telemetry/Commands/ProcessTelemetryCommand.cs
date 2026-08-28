using MediatR;
using ShipmentTelemetry.Domain.Enums;

namespace ShipmentTelemetry.Application.Telemetry.Commands;

public sealed record ProcessTelemetryCommand(
    Guid EventId,
    string ContainerId,
    string ShipmentId,
    TelemetryEventType EventType,
    long SequenceNumber,
    DateTimeOffset DeviceTimestamp,
    string? DeviceId,
    string? LocationName,
    double? Latitude,
    double? Longitude,
    string PayloadJson) : IRequest<ProcessTelemetryResult>;

public sealed record ProcessTelemetryResult(
    TelemetryProcessingOutcome Outcome,
    string? Reason,
    OperationalMilestone? CurrentMilestone,
    bool ShouldRetry = false)
{
    public static ProcessTelemetryResult Retry() =>
        new(TelemetryProcessingOutcome.Quarantined, null, null, ShouldRetry: true);
}
