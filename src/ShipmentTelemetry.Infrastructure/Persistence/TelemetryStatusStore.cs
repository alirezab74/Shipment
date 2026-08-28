using Microsoft.EntityFrameworkCore;
using ShipmentTelemetry.Application.Abstractions;
using ShipmentTelemetry.Domain.Enums;
using ShipmentTelemetry.Domain.ValueObjects;
using ShipmentTelemetry.Infrastructure.Persistence.Entities;

namespace ShipmentTelemetry.Infrastructure.Persistence;

public sealed class TelemetryStatusStore : ITelemetryStatusStore
{
    private readonly ShipmentTelemetryDbContext _dbContext;

    public TelemetryStatusStore(ShipmentTelemetryDbContext dbContext) => _dbContext = dbContext;

    public async Task SaveAsync(TelemetryStatusRecord record, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.TelemetryStatuses
            .FirstOrDefaultAsync(x => x.EventId == record.EventId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await _dbContext.TelemetryStatuses.AddAsync(
                new TelemetryStatusEntity
                {
                    EventId = record.EventId.Value,
                    Outcome = (int)record.Outcome,
                    Reason = record.Reason,
                    CurrentMilestone = record.CurrentMilestone.HasValue
                        ? (int)record.CurrentMilestone.Value
                        : null,
                    ProcessedAt = record.ProcessedAt
                },
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existing.Outcome = (int)record.Outcome;
            existing.Reason = record.Reason;
            existing.CurrentMilestone = record.CurrentMilestone.HasValue
                ? (int)record.CurrentMilestone.Value
                : null;
            existing.ProcessedAt = record.ProcessedAt;
        }
    }

    public async Task<TelemetryStatusRecord?> GetByEventIdAsync(
        TelemetryEventId eventId,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.TelemetryStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EventId == eventId.Value, cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : new TelemetryStatusRecord(
                new TelemetryEventId(entity.EventId),
                (TelemetryProcessingOutcome)entity.Outcome,
                entity.Reason,
                entity.CurrentMilestone.HasValue
                    ? (OperationalMilestone)entity.CurrentMilestone.Value
                    : null,
                entity.ProcessedAt);
    }
}
