using MediatR;
using ShipmentTelemetry.Application.Abstractions;
using ShipmentTelemetry.Domain.ValueObjects;

namespace ShipmentTelemetry.Application.Shipments.Queries;

public sealed record GetShipmentOperationalStateQuery(string ShipmentId)
    : IRequest<ShipmentOperationalStateDto?>;

public sealed record ShipmentOperationalStateDto(
    string ShipmentId,
    string ContainerId,
    string CurrentMilestone,
    long LastAcceptedSequence,
    uint Version,
    DateTimeOffset UpdatedAt);

public sealed class GetShipmentOperationalStateQueryHandler
    : IRequestHandler<GetShipmentOperationalStateQuery, ShipmentOperationalStateDto?>
{
    private readonly IOperationalStateReadModel _readModel;
    private readonly IShipmentOperationalStateCache _cache;

    public GetShipmentOperationalStateQueryHandler(
        IOperationalStateReadModel readModel,
        IShipmentOperationalStateCache cache)
    {
        _readModel = readModel;
        _cache = cache;
    }

    public async Task<ShipmentOperationalStateDto?> Handle(
        GetShipmentOperationalStateQuery request,
        CancellationToken cancellationToken)
    {
        var shipmentId = new ShipmentId(request.ShipmentId);

        var cached = await _cache.GetAsync(shipmentId, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        var snapshot = await _readModel
            .GetByShipmentIdAsync(shipmentId, cancellationToken)
            .ConfigureAwait(false);

        if (snapshot is null)
        {
            return null;
        }

        var dto = new ShipmentOperationalStateDto(
            snapshot.ShipmentId,
            snapshot.ContainerId,
            snapshot.CurrentMilestone.ToString(),
            snapshot.LastAcceptedSequence,
            snapshot.Version,
            snapshot.UpdatedAt);

        await _cache.SetAsync(shipmentId, dto, cancellationToken).ConfigureAwait(false);
        return dto;
    }
}
