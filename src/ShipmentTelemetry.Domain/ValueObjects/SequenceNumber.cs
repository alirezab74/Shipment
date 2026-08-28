namespace ShipmentTelemetry.Domain.ValueObjects;

public sealed record SequenceNumber
{
    public long Value { get; }

    public SequenceNumber(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Sequence number cannot be negative.");
        }

        Value = value;
    }

    public bool IsStaleComparedTo(long lastAccepted) => Value < lastAccepted;

    public bool IsDuplicateSequence(long lastAccepted) => Value == lastAccepted;

    public override string ToString() => Value.ToString();
}
