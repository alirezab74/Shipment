using ShipmentTelemetry.Domain.Aggregates;
using ShipmentTelemetry.Domain.ValueObjects;

namespace ShipmentTelemetry.Domain.Repositories;

public interface IShipmentOperationalStateRepository
{
    Task<ShipmentOperationalState?> GetByShipmentIdAsync(
        ShipmentId shipmentId,
        CancellationToken cancellationToken);

    Task AddAsync(ShipmentOperationalState state, CancellationToken cancellationToken);

    Task UpdateAsync(ShipmentOperationalState state, CancellationToken cancellationToken);
}
