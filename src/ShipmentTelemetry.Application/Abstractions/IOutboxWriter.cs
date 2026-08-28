namespace ShipmentTelemetry.Application.Abstractions;

public interface IOutboxWriter
{
    Task EnqueueAsync(string messageType, string payloadJson, CancellationToken cancellationToken);
}
