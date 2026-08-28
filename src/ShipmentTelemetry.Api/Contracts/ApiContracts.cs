using System.ComponentModel.DataAnnotations;
using ShipmentTelemetry.Domain.Enums;

namespace ShipmentTelemetry.Api.Contracts;

public sealed class IngestTelemetryRequest
{
    [Required]
    public Guid EventId { get; init; }

    [Required]
    [MaxLength(64)]
    public string ContainerId { get; init; } = null!;

    [Required]
    [MaxLength(64)]
    public string ShipmentId { get; init; } = null!;

    [Required]
    public TelemetryEventType EventType { get; init; }

    [Required]
    [Range(0, long.MaxValue)]
    public long SequenceNumber { get; init; }

    [Required]
    public DateTimeOffset DeviceTimestamp { get; init; }

    [MaxLength(128)]
    public string? DeviceId { get; init; }

    [MaxLength(256)]
    public string? LocationName { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string PayloadJson { get; init; } = "{}";
}

public sealed class TelemetryProcessingResponse
{
    public required string Outcome { get; init; }

    public string? Reason { get; init; }

    public string? CurrentMilestone { get; init; }

    public string? TraceId { get; init; }
}

public sealed class ShipmentOperationalStateResponse
{
    public required string ShipmentId { get; init; }

    public required string ContainerId { get; init; }

    public required string CurrentMilestone { get; init; }

    public long LastAcceptedSequence { get; init; }

    public uint Version { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class TelemetryStatusResponse
{
    public required string EventId { get; init; }

    public required string Outcome { get; init; }

    public string? Reason { get; init; }

    public string? CurrentMilestone { get; init; }

    public DateTimeOffset ProcessedAt { get; init; }
}
