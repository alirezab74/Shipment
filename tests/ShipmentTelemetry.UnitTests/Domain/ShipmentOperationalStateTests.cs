using FluentAssertions;
using ShipmentTelemetry.Domain.Aggregates;
using ShipmentTelemetry.Domain.Enums;
using ShipmentTelemetry.Domain.Models;
using ShipmentTelemetry.Domain.ValueObjects;

namespace ShipmentTelemetry.UnitTests.Domain;

public sealed class ShipmentOperationalStateTests
{
    private static TelemetryEnvelope CreateEnvelope(
        Guid eventId,
        long sequence,
        TelemetryEventType eventType,
        string payload = "{}") =>
        new(
            new TelemetryEventId(eventId),
            new ContainerId("CONT-001"),
            new ShipmentId("SHP-9001"),
            eventType,
            new SequenceNumber(sequence),
            DateTimeOffset.UtcNow,
            "DEV-1",
            new Location("Port A", 1, 2),
            payload,
            new PayloadHash("hash"));

    [Fact]
    public void ProcessTelemetry_AcceptsValidFirstMilestone()
    {
        var state = ShipmentOperationalState.Create(new ShipmentId("SHP-9001"), new ContainerId("CONT-001"));
        var envelope = CreateEnvelope(Guid.NewGuid(), 1, TelemetryEventType.LocationReported);

        var result = state.ProcessTelemetry(envelope);

        result.Outcome.Should().Be(TelemetryProcessingOutcome.Accepted);
        state.CurrentMilestone.Should().Be(OperationalMilestone.ArrivedAtPort);
        state.LastAcceptedSequence.Should().Be(1);
    }

    [Fact]
    public void ProcessTelemetry_RejectsStaleSequence()
    {
        var state = ShipmentOperationalState.Restore(
            new ShipmentId("SHP-9001"),
            new ContainerId("CONT-001"),
            OperationalMilestone.GateIn,
            105,
            1,
            DateTimeOffset.UtcNow);

        var envelope = CreateEnvelope(Guid.NewGuid(), 104, TelemetryEventType.LoadConfirmed);
        var result = state.ProcessTelemetry(envelope);

        result.Outcome.Should().Be(TelemetryProcessingOutcome.Stale);
        state.CurrentMilestone.Should().Be(OperationalMilestone.GateIn);
        state.LastAcceptedSequence.Should().Be(105);
    }

    [Fact]
    public void ProcessTelemetry_RejectsInvalidTransition()
    {
        var state = ShipmentOperationalState.Restore(
            new ShipmentId("SHP-9001"),
            new ContainerId("CONT-001"),
            OperationalMilestone.GateIn,
            2,
            1,
            DateTimeOffset.UtcNow);

        var envelope = CreateEnvelope(Guid.NewGuid(), 3, TelemetryEventType.GateOutDetected);
        var result = state.ProcessTelemetry(envelope);

        result.Outcome.Should().Be(TelemetryProcessingOutcome.InvalidTransition);
        state.CurrentMilestone.Should().Be(OperationalMilestone.GateIn);
    }

    [Theory]
    [InlineData(OperationalMilestone.ArrivedAtPort)]
    [InlineData(OperationalMilestone.GateIn)]
    [InlineData(OperationalMilestone.LoadedOnVessel)]
    [InlineData(OperationalMilestone.DepartedPort)]
    [InlineData(OperationalMilestone.GateOut)]
    public void ProcessTelemetry_AcceptsSequentialMilestones(OperationalMilestone expected)
    {
        var state = ShipmentOperationalState.Create(new ShipmentId("SHP-9001"), new ContainerId("CONT-001"));
        var sequence = 1L;

        foreach (OperationalMilestone milestone in Enum.GetValues<OperationalMilestone>())
        {
            if (milestone == OperationalMilestone.None || (int)milestone > (int)expected)
            {
                continue;
            }

            var type = milestone switch
            {
                OperationalMilestone.ArrivedAtPort => TelemetryEventType.LocationReported,
                OperationalMilestone.GateIn => TelemetryEventType.GateInDetected,
                OperationalMilestone.LoadedOnVessel => TelemetryEventType.LoadConfirmed,
                OperationalMilestone.DepartedPort => TelemetryEventType.VesselDepartureDetected,
                OperationalMilestone.GateOut => TelemetryEventType.GateOutDetected,
                _ => throw new InvalidOperationException()
            };

            var result = state.ProcessTelemetry(CreateEnvelope(Guid.NewGuid(), sequence++, type));
            result.Outcome.Should().Be(TelemetryProcessingOutcome.Accepted);
        }

        state.CurrentMilestone.Should().Be(expected);
    }
}

public sealed class MilestoneTransitionRulesTests
{
    [Theory]
    [InlineData(OperationalMilestone.None, OperationalMilestone.ArrivedAtPort, true)]
    [InlineData(OperationalMilestone.ArrivedAtPort, OperationalMilestone.GateIn, true)]
    [InlineData(OperationalMilestone.GateIn, OperationalMilestone.LoadedOnVessel, true)]
    [InlineData(OperationalMilestone.GateIn, OperationalMilestone.GateOut, false)]
    [InlineData(OperationalMilestone.GateOut, OperationalMilestone.ArrivedAtPort, false)]
    public void CanTransition_EnforcesSingleStepFlow(
        OperationalMilestone current,
        OperationalMilestone target,
        bool expected)
    {
        MilestoneTransitionRules.CanTransition(current, target).Should().Be(expected);
    }
}
