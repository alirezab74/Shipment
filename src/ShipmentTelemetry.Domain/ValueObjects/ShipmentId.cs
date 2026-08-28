namespace ShipmentTelemetry.Domain.ValueObjects;

public sealed record ShipmentId
{
    public string Value { get; }

    public ShipmentId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Shipment id is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
