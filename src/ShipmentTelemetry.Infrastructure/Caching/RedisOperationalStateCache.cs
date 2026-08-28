using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShipmentTelemetry.Application.Abstractions;
using ShipmentTelemetry.Application.Shipments.Queries;
using ShipmentTelemetry.Domain.ValueObjects;
using StackExchange.Redis;

namespace ShipmentTelemetry.Infrastructure.Caching;

public sealed class RedisOperationalStateCache : IShipmentOperationalStateCache
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IConnectionMultiplexer? _connection;
    private readonly ILogger<RedisOperationalStateCache> _logger;

    public RedisOperationalStateCache(
        IConnectionMultiplexer? connection,
        ILogger<RedisOperationalStateCache> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<ShipmentOperationalStateDto?> GetAsync(
        ShipmentId shipmentId,
        CancellationToken cancellationToken)
    {
        if (_connection is null || !_connection.IsConnected)
        {
            return null;
        }

        try
        {
            var value = await _connection.GetDatabase()
                .StringGetAsync(CacheKey(shipmentId))
                .ConfigureAwait(false);

            return value.HasValue
                ? JsonSerializer.Deserialize<ShipmentOperationalStateDto>(value!)
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis read failed for shipment {ShipmentId}", shipmentId);
            return null;
        }
    }

    public async Task SetAsync(
        ShipmentId shipmentId,
        ShipmentOperationalStateDto dto,
        CancellationToken cancellationToken)
    {
        if (_connection is null || !_connection.IsConnected)
        {
            return;
        }

        try
        {
            await _connection.GetDatabase().StringSetAsync(
                CacheKey(shipmentId),
                JsonSerializer.Serialize(dto),
                CacheTtl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis write failed for shipment {ShipmentId}", shipmentId);
        }
    }

    public async Task InvalidateAsync(ShipmentId shipmentId, CancellationToken cancellationToken)
    {
        if (_connection is null || !_connection.IsConnected)
        {
            return;
        }

        try
        {
            await _connection.GetDatabase().KeyDeleteAsync(CacheKey(shipmentId)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis invalidate failed for shipment {ShipmentId}", shipmentId);
        }
    }

    private static string CacheKey(ShipmentId shipmentId) => $"shipment:state:{shipmentId.Value}";
}

public sealed class NoOpOperationalStateCache : IShipmentOperationalStateCache
{
    public Task<ShipmentOperationalStateDto?> GetAsync(
        ShipmentId shipmentId,
        CancellationToken cancellationToken) =>
        Task.FromResult<ShipmentOperationalStateDto?>(null);

    public Task SetAsync(
        ShipmentId shipmentId,
        ShipmentOperationalStateDto dto,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task InvalidateAsync(ShipmentId shipmentId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
