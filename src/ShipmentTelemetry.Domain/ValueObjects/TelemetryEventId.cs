namespace ShipmentTelemetry.Domain.ValueObjects;

public sealed record TelemetryEventId
{
    public Guid Value { get; }

    public TelemetryEventId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Event id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static TelemetryEventId Parse(string value) => new(Guid.Parse(value));

    public override string ToString() => Value.ToString();
}
