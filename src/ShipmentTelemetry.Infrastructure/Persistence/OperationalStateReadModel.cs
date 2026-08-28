using Microsoft.EntityFrameworkCore;
using ShipmentTelemetry.Application.Abstractions;
using ShipmentTelemetry.Domain.Enums;
using ShipmentTelemetry.Domain.ValueObjects;
using ShipmentTelemetry.Infrastructure.Persistence.Entities;

namespace ShipmentTelemetry.Infrastructure.Persistence;

public sealed class OperationalStateReadModel : IOperationalStateReadModel
{
    private readonly ShipmentTelemetryDbContext _dbContext;

    public OperationalStateReadModel(ShipmentTelemetryDbContext dbContext) => _dbContext = dbContext;

    public async Task UpsertAsync(
        ShipmentId shipmentId,
        ContainerId containerId,
        OperationalMilestone milestone,
        long lastAcceptedSequence,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ShipmentOperationalReadModels
            .FirstOrDefaultAsync(x => x.ShipmentId == shipmentId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            await _dbContext.ShipmentOperationalReadModels.AddAsync(
                new ShipmentOperationalReadModelEntity
                {
                    ShipmentId = shipmentId.Value,
                    ContainerId = containerId.Value,
                    CurrentMilestone = (int)milestone,
                    LastAcceptedSequence = lastAcceptedSequence,
                    Version = 1,
                    UpdatedAt = updatedAt
                },
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            entity.ContainerId = containerId.Value;
            entity.CurrentMilestone = (int)milestone;
            entity.LastAcceptedSequence = lastAcceptedSequence;
            entity.Version++;
            entity.UpdatedAt = updatedAt;
        }
    }

    public async Task<OperationalStateSnapshot?> GetByShipmentIdAsync(
        ShipmentId shipmentId,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ShipmentOperationalReadModels
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ShipmentId == shipmentId.Value, cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : new OperationalStateSnapshot(
                entity.ShipmentId,
                entity.ContainerId,
                (OperationalMilestone)entity.CurrentMilestone,
                entity.LastAcceptedSequence,
                entity.Version,
                entity.UpdatedAt);
    }
}
