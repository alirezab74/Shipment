namespace ShipmentTelemetry.Domain.ValueObjects;

public sealed record ContainerId
{
    public string Value { get; }

    public ContainerId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Container id is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
