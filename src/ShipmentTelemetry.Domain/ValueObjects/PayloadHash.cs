namespace ShipmentTelemetry.Domain.ValueObjects;

public sealed record PayloadHash
{
    public string Value { get; }

    public PayloadHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Payload hash is required.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}
