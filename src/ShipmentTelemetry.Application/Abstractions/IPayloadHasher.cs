namespace ShipmentTelemetry.Application.Abstractions;

public interface IPayloadHasher
{
    string ComputeHash(string payloadJson);
}
