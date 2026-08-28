namespace ShipmentTelemetry.Domain.Enums;

public enum TelemetryProcessingOutcome
{
    Accepted = 1,
    Duplicate = 2,
    Stale = 3,
    SequenceConflict = 4,
    PayloadConflict = 5,
    InvalidTransition = 6,
    Quarantined = 7
}
