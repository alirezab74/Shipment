using ShipmentTelemetry.Domain.Enums;
using ShipmentTelemetry.Domain.ValueObjects;

namespace ShipmentTelemetry.Domain.Models;

public sealed record TelemetryEnvelope(
    TelemetryEventId EventId,
    ContainerId ContainerId,
    ShipmentId ShipmentId,
    TelemetryEventType EventType,
    SequenceNumber SequenceNumber,
    DateTimeOffset DeviceTimestamp,
    string? DeviceId,
    Location Location,
    string PayloadJson,
    PayloadHash PayloadHash);
