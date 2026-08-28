using Microsoft.EntityFrameworkCore;
using ShipmentTelemetry.Infrastructure.Persistence.Entities;

namespace ShipmentTelemetry.Infrastructure.Persistence;

public sealed class ShipmentTelemetryDbContext : DbContext
{
    public ShipmentTelemetryDbContext(DbContextOptions<ShipmentTelemetryDbContext> options)
        : base(options)
    {
    }

    public DbSet<ShipmentOperationalStateEntity> ShipmentOperationalStates => Set<ShipmentOperationalStateEntity>();

    public DbSet<ProcessedTelemetryEntity> ProcessedTelemetry => Set<ProcessedTelemetryEntity>();

    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    public DbSet<TelemetryStatusEntity> TelemetryStatuses => Set<TelemetryStatusEntity>();

    public DbSet<ShipmentOperationalReadModelEntity> ShipmentOperationalReadModels =>
        Set<ShipmentOperationalReadModelEntity>();

    public DbSet<ProcessedIntegrationMessageEntity> ProcessedIntegrationMessages =>
        Set<ProcessedIntegrationMessageEntity>();

    public DbSet<DownstreamMilestoneNotificationEntity> DownstreamMilestoneNotifications =>
        Set<DownstreamMilestoneNotificationEntity>();

    public DbSet<QuarantinedTelemetryEntity> QuarantinedTelemetry => Set<QuarantinedTelemetryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShipmentOperationalStateEntity>(entity =>
        {
            entity.ToTable("shipment_operational_states");
            entity.HasKey(x => x.ShipmentId);
            entity.Property(x => x.ShipmentId).HasMaxLength(64);
            entity.Property(x => x.ContainerId).HasMaxLength(64);
            entity.Property(x => x.Version).IsConcurrencyToken();
            entity.HasIndex(x => x.ContainerId);
        });

        modelBuilder.Entity<ProcessedTelemetryEntity>(entity =>
        {
            entity.ToTable("processed_telemetry");
            entity.HasKey(x => x.EventId);
            entity.HasIndex(x => x.EventId).IsUnique();
            entity.HasIndex(x => new { x.ContainerId, x.SequenceNumber }).IsUnique();
            entity.HasIndex(x => x.ProcessedAt);
            entity.Property(x => x.ContainerId).HasMaxLength(64);
            entity.Property(x => x.ShipmentId).HasMaxLength(64);
            entity.Property(x => x.PayloadHash).HasMaxLength(128);
        });

        modelBuilder.Entity<OutboxMessageEntity>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Status, x.CreatedAt });
            entity.Property(x => x.MessageType).HasMaxLength(256);
        });

        modelBuilder.Entity<TelemetryStatusEntity>(entity =>
        {
            entity.ToTable("telemetry_statuses");
            entity.HasKey(x => x.EventId);
        });

        modelBuilder.Entity<ShipmentOperationalReadModelEntity>(entity =>
        {
            entity.ToTable("shipment_operational_read_models");
            entity.HasKey(x => x.ShipmentId);
            entity.Property(x => x.ShipmentId).HasMaxLength(64);
            entity.Property(x => x.ContainerId).HasMaxLength(64);
        });

        modelBuilder.Entity<ProcessedIntegrationMessageEntity>(entity =>
        {
            entity.ToTable("processed_integration_messages");
            entity.HasKey(x => x.MessageId);
            entity.HasIndex(x => x.MessageId).IsUnique();
        });

        modelBuilder.Entity<DownstreamMilestoneNotificationEntity>(entity =>
        {
            entity.ToTable("downstream_milestone_notifications");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.IntegrationEventId).IsUnique();
        });

        modelBuilder.Entity<QuarantinedTelemetryEntity>(entity =>
        {
            entity.ToTable("quarantined_telemetry");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.EventId);
        });
    }
}
