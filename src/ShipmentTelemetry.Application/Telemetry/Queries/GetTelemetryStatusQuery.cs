using MediatR;
using ShipmentTelemetry.Application.Abstractions;
using ShipmentTelemetry.Domain.ValueObjects;

namespace ShipmentTelemetry.Application.Telemetry.Queries;

public sealed record GetTelemetryStatusQuery(string EventId) : IRequest<TelemetryStatusDto?>;

public sealed record TelemetryStatusDto(
    string EventId,
    string Outcome,
    string? Reason,
    string? CurrentMilestone,
    DateTimeOffset ProcessedAt);

public sealed class GetTelemetryStatusQueryHandler
    : IRequestHandler<GetTelemetryStatusQuery, TelemetryStatusDto?>
{
    private readonly ITelemetryStatusStore _store;

    public GetTelemetryStatusQueryHandler(ITelemetryStatusStore store) => _store = store;

    public async Task<TelemetryStatusDto?> Handle(
        GetTelemetryStatusQuery request,
        CancellationToken cancellationToken)
    {
        var record = await _store
            .GetByEventIdAsync(TelemetryEventId.Parse(request.EventId), cancellationToken)
            .ConfigureAwait(false);

        return record is null
            ? null
            : new TelemetryStatusDto(
                record.EventId.Value.ToString(),
                record.Outcome.ToString(),
                record.Reason,
                record.CurrentMilestone?.ToString(),
                record.ProcessedAt);
    }
}
