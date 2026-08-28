namespace ShipmentTelemetry.Domain.ValueObjects;

public sealed record Location(string? Name, double? Latitude, double? Longitude)
{
    public static Location Empty => new(null, null, null);
}
