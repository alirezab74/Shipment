using Microsoft.EntityFrameworkCore;
using ShipmentTelemetry.Domain.Aggregates;
using ShipmentTelemetry.Domain.Enums;
using ShipmentTelemetry.Domain.Repositories;
using ShipmentTelemetry.Domain.ValueObjects;
using ShipmentTelemetry.Infrastructure.Persistence.Entities;

namespace ShipmentTelemetry.Infrastructure.Persistence.Repositories;

public sealed class ShipmentOperationalStateRepository : IShipmentOperationalStateRepository
{
    private readonly ShipmentTelemetryDbContext _dbContext;

    public ShipmentOperationalStateRepository(ShipmentTelemetryDbContext dbContext) =>
        _dbContext = dbContext;

    public async Task<ShipmentOperationalState?> GetByShipmentIdAsync(
        ShipmentId shipmentId,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ShipmentOperationalStates
            .AsTracking()
            .FirstOrDefaultAsync(x => x.ShipmentId == shipmentId.Value, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToAggregate(entity);
    }

    public async Task AddAsync(ShipmentOperationalState state, CancellationToken cancellationToken)
    {
        var entity = MapToEntity(state);
        await _dbContext.ShipmentOperationalStates.AddAsync(entity, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpdateAsync(ShipmentOperationalState state, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ShipmentOperationalStates
            .FirstAsync(x => x.ShipmentId == state.ShipmentId.Value, cancellationToken)
            .ConfigureAwait(false);

        UpdateEntity(entity, state);
    }

    internal static ShipmentOperationalState MapToAggregate(ShipmentOperationalStateEntity entity) =>
        ShipmentOperationalState.Restore(
            new ShipmentId(entity.ShipmentId),
            new ContainerId(entity.ContainerId),
            (OperationalMilestone)entity.CurrentMilestone,
            entity.LastAcceptedSequence,
            entity.Version,
            entity.UpdatedAt);

    internal static ShipmentOperationalStateEntity MapToEntity(ShipmentOperationalState state) =>
        new()
        {
            ShipmentId = state.ShipmentId.Value,
            ContainerId = state.ContainerId.Value,
            CurrentMilestone = (int)state.CurrentMilestone,
            LastAcceptedSequence = state.LastAcceptedSequence,
            Version = state.Version,
            UpdatedAt = state.UpdatedAt
        };

    internal static void UpdateEntity(ShipmentOperationalStateEntity entity, ShipmentOperationalState state)
    {
        entity.ContainerId = state.ContainerId.Value;
        entity.CurrentMilestone = (int)state.CurrentMilestone;
        entity.LastAcceptedSequence = state.LastAcceptedSequence;
        entity.Version = state.Version;
        entity.UpdatedAt = state.UpdatedAt;
    }
}
