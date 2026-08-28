using ShipmentTelemetry.Domain.ValueObjects;

namespace ShipmentTelemetry.Application.Abstractions;

public interface IShipmentOperationalStateCache
{
    Task<Shipments.Queries.ShipmentOperationalStateDto?> GetAsync(
        ShipmentId shipmentId,
        CancellationToken cancellationToken);

    Task SetAsync(
        ShipmentId shipmentId,
        Shipments.Queries.ShipmentOperationalStateDto dto,
        CancellationToken cancellationToken);

    Task InvalidateAsync(ShipmentId shipmentId, CancellationToken cancellationToken);
}
