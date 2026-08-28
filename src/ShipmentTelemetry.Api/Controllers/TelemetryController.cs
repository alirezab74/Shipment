using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShipmentTelemetry.Api.Contracts;
using ShipmentTelemetry.Application.Telemetry.Commands;
using ShipmentTelemetry.Domain.Enums;

namespace ShipmentTelemetry.Api.Controllers;

[ApiController]
[Route("api/telemetry")]
public sealed class TelemetryController : ControllerBase
{
    private readonly IMediator _mediator;

    public TelemetryController(IMediator mediator) => _mediator = mediator;

    [HttpPost("events")]
    [ProducesResponseType(typeof(TelemetryProcessingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TelemetryProcessingResponse>> IngestAsync(
        [FromBody] IngestTelemetryRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ProcessTelemetryCommand(
            request.EventId,
            request.ContainerId,
            request.ShipmentId,
            request.EventType,
            request.SequenceNumber,
            request.DeviceTimestamp,
            request.DeviceId,
            request.LocationName,
            request.Latitude,
            request.Longitude,
            request.PayloadJson);

        var result = await _mediator.Send(command, cancellationToken).ConfigureAwait(false);
        var traceId = HttpContext.TraceIdentifier;

        var response = new TelemetryProcessingResponse
        {
            Outcome = result.Outcome.ToString(),
            Reason = result.Reason,
            CurrentMilestone = result.CurrentMilestone?.ToString(),
            TraceId = traceId
        };

        return result.Outcome switch
        {
            TelemetryProcessingOutcome.Accepted => Ok(response),
            TelemetryProcessingOutcome.Duplicate => Ok(response),
            TelemetryProcessingOutcome.Stale => Ok(response),
            TelemetryProcessingOutcome.PayloadConflict => Conflict(response),
            TelemetryProcessingOutcome.SequenceConflict => Conflict(response),
            TelemetryProcessingOutcome.InvalidTransition => UnprocessableEntity(response),
            TelemetryProcessingOutcome.Quarantined => UnprocessableEntity(response),
            _ => Ok(response)
        };
    }

    [HttpGet("events/{eventId:guid}/status")]
    [ProducesResponseType(typeof(TelemetryStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TelemetryStatusResponse>> GetStatusAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var status = await _mediator
            .Send(new Application.Telemetry.Queries.GetTelemetryStatusQuery(eventId.ToString()), cancellationToken)
            .ConfigureAwait(false);

        if (status is null)
        {
            return NotFound();
        }

        return Ok(new TelemetryStatusResponse
        {
            EventId = status.EventId,
            Outcome = status.Outcome,
            Reason = status.Reason,
            CurrentMilestone = status.CurrentMilestone,
            ProcessedAt = status.ProcessedAt
        });
    }
}
