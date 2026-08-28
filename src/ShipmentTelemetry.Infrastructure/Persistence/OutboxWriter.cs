using ShipmentTelemetry.Application.Abstractions;
using ShipmentTelemetry.Infrastructure.Persistence.Entities;

namespace ShipmentTelemetry.Infrastructure.Persistence;

public sealed class OutboxWriter : IOutboxWriter
{
    private readonly ShipmentTelemetryDbContext _dbContext;

    public OutboxWriter(ShipmentTelemetryDbContext dbContext) => _dbContext = dbContext;

    public async Task EnqueueAsync(
        string messageType,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        await _dbContext.OutboxMessages.AddAsync(
            new OutboxMessageEntity
            {
                Id = Guid.NewGuid(),
                MessageType = messageType,
                PayloadJson = payloadJson,
                Status = (int)OutboxMessageStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                RetryCount = 0
            },
            cancellationToken).ConfigureAwait(false);
    }
}

public enum OutboxMessageStatus
{
    Pending = 1,
    Published = 2,
    Failed = 3,
    Quarantined = 4
}
