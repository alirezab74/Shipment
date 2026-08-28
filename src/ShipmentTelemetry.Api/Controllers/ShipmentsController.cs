using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShipmentTelemetry.Api.Contracts;
using ShipmentTelemetry.Application.Shipments.Queries;

namespace ShipmentTelemetry.Api.Controllers;

[ApiController]
[Route("api/shipments")]
public sealed class ShipmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ShipmentsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{shipmentId}/operational-state")]
    [ProducesResponseType(typeof(ShipmentOperationalStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShipmentOperationalStateResponse>> GetOperationalStateAsync(
        string shipmentId,
        CancellationToken cancellationToken)
    {
        var state = await _mediator
            .Send(new GetShipmentOperationalStateQuery(shipmentId), cancellationToken)
            .ConfigureAwait(false);

        if (state is null)
        {
            return NotFound();
        }

        return Ok(new ShipmentOperationalStateResponse
        {
            ShipmentId = state.ShipmentId,
            ContainerId = state.ContainerId,
            CurrentMilestone = state.CurrentMilestone,
            LastAcceptedSequence = state.LastAcceptedSequence,
            Version = state.Version,
            UpdatedAt = state.UpdatedAt
        });
    }
}
