namespace ShipmentTelemetry.Infrastructure.Persistence.Entities;

public sealed class ShipmentOperationalStateEntity
{
    public string ShipmentId { get; set; } = null!;

    public string ContainerId { get; set; } = null!;

    public int CurrentMilestone { get; set; }

    public long LastAcceptedSequence { get; set; }

    public uint Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ProcessedTelemetryEntity
{
    public Guid EventId { get; set; }

    public string ContainerId { get; set; } = null!;

    public string ShipmentId { get; set; } = null!;

    public long SequenceNumber { get; set; }

    public string PayloadHash { get; set; } = null!;

    public int Outcome { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}

public sealed class OutboxMessageEntity
{
    public Guid Id { get; set; }

    public string MessageType { get; set; } = null!;

    public string PayloadJson { get; set; } = null!;

    public int Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int RetryCount { get; set; }

    public string? LastError { get; set; }
}

public sealed class TelemetryStatusEntity
{
    public Guid EventId { get; set; }

    public int Outcome { get; set; }

    public string? Reason { get; set; }

    public int? CurrentMilestone { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}

public sealed class ShipmentOperationalReadModelEntity
{
    public string ShipmentId { get; set; } = null!;

    public string ContainerId { get; set; } = null!;

    public int CurrentMilestone { get; set; }

    public long LastAcceptedSequence { get; set; }

    public uint Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ProcessedIntegrationMessageEntity
{
    public Guid MessageId { get; set; }

    public string MessageType { get; set; } = null!;

    public DateTimeOffset ProcessedAt { get; set; }
}

public sealed class DownstreamMilestoneNotificationEntity
{
    public Guid Id { get; set; }

    public Guid IntegrationEventId { get; set; }

    public string ShipmentId { get; set; } = null!;

    public string Milestone { get; set; } = null!;

    public DateTimeOffset NotifiedAt { get; set; }
}

public sealed class QuarantinedTelemetryEntity
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public string ContainerId { get; set; } = null!;

    public string ShipmentId { get; set; } = null!;

    public long SequenceNumber { get; set; }

    public string Reason { get; set; } = null!;

    public string PayloadJson { get; set; } = null!;

    public DateTimeOffset QuarantinedAt { get; set; }
}
