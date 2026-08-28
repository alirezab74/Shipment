using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShipmentTelemetry.Contracts.IntegrationEvents;
using ShipmentTelemetry.Infrastructure.Persistence;
using ShipmentTelemetry.Infrastructure.Persistence.Entities;

namespace ShipmentTelemetry.Infrastructure.Messaging;

public sealed class ShipmentMilestoneRecordedConsumer : ICapSubscribe
{
    private readonly ShipmentTelemetryDbContext _dbContext;
    private readonly ILogger<ShipmentMilestoneRecordedConsumer> _logger;

    public ShipmentMilestoneRecordedConsumer(
        ShipmentTelemetryDbContext dbContext,
        ILogger<ShipmentMilestoneRecordedConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [CapSubscribe(nameof(ShipmentMilestoneRecordedIntegrationEvent), Group = "shipment-milestone-downstream")]
    public async Task HandleAsync(
        ShipmentMilestoneRecordedIntegrationEvent message,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await _dbContext.ProcessedIntegrationMessages.AddAsync(
                new ProcessedIntegrationMessageEntity
                {
                    MessageId = message.EventId,
                    MessageType = nameof(ShipmentMilestoneRecordedIntegrationEvent),
                    ProcessedAt = DateTimeOffset.UtcNow
                },
                cancellationToken).ConfigureAwait(false);

            await _dbContext.DownstreamMilestoneNotifications.AddAsync(
                new DownstreamMilestoneNotificationEntity
                {
                    Id = Guid.NewGuid(),
                    IntegrationEventId = message.EventId,
                    ShipmentId = message.ShipmentId,
                    Milestone = message.Milestone,
                    NotifiedAt = DateTimeOffset.UtcNow
                },
                cancellationToken).ConfigureAwait(false);

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Downstream milestone notification recorded. EventId={EventId} ShipmentId={ShipmentId} Milestone={Milestone}",
                message.EventId,
                message.ShipmentId,
                message.Milestone);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                ex,
                "Duplicate integration event ignored. EventId={EventId}",
                message.EventId);
        }
    }
}
