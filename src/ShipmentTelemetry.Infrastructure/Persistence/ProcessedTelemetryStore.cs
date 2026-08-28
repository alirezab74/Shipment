using Microsoft.EntityFrameworkCore;
using ShipmentTelemetry.Application.Abstractions;
using ShipmentTelemetry.Domain.Enums;
using ShipmentTelemetry.Domain.ValueObjects;
using ShipmentTelemetry.Infrastructure.Persistence.Entities;

namespace ShipmentTelemetry.Infrastructure.Persistence;

public sealed class ProcessedTelemetryStore : IProcessedTelemetryStore
{
    private readonly ShipmentTelemetryDbContext _dbContext;

    public ProcessedTelemetryStore(ShipmentTelemetryDbContext dbContext) => _dbContext = dbContext;

    public async Task<ProcessedTelemetryRecord?> FindByEventIdAsync(
        TelemetryEventId eventId,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ProcessedTelemetry
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EventId == eventId.Value, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : Map(entity);
    }

    public async Task<ProcessedTelemetryRecord?> FindByContainerSequenceAsync(
        ContainerId containerId,
        long sequenceNumber,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ProcessedTelemetry
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.ContainerId == containerId.Value && x.SequenceNumber == sequenceNumber,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : Map(entity);
    }

    public async Task AddAsync(ProcessedTelemetryRecord record, CancellationToken cancellationToken)
    {
        await _dbContext.ProcessedTelemetry.AddAsync(
            new ProcessedTelemetryEntity
            {
                EventId = record.EventId.Value,
                ContainerId = record.ContainerId.Value,
                ShipmentId = record.ShipmentId.Value,
                SequenceNumber = record.SequenceNumber,
                PayloadHash = record.PayloadHash,
                Outcome = (int)record.Outcome,
                ProcessedAt = record.ProcessedAt
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static ProcessedTelemetryRecord Map(ProcessedTelemetryEntity entity) =>
        new(
            new TelemetryEventId(entity.EventId),
            new ContainerId(entity.ContainerId),
            new ShipmentId(entity.ShipmentId),
            entity.SequenceNumber,
            entity.PayloadHash,
            (TelemetryProcessingOutcome)entity.Outcome,
            entity.ProcessedAt);
}
