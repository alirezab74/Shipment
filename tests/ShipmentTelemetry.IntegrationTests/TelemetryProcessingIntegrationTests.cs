using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShipmentTelemetry.Application.Telemetry.Commands;
using ShipmentTelemetry.Contracts.IntegrationEvents;
using ShipmentTelemetry.Domain.Enums;
using ShipmentTelemetry.Infrastructure.Messaging;
using ShipmentTelemetry.Infrastructure.Persistence;
using ShipmentTelemetry.Infrastructure.Persistence.Entities;
using ShipmentTelemetry.IntegrationTests.Infrastructure;

namespace ShipmentTelemetry.IntegrationTests;

[Collection(nameof(PostgresIntegrationCollection))]
public sealed class TelemetryProcessingIntegrationTests
{
    private readonly PostgresIntegrationFixture _fixture;

    public TelemetryProcessingIntegrationTests(PostgresIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DuplicateEventId_ProducesOneBusinessEffect()
    {
        await _fixture.ResetDatabaseAsync();
        var serviceProvider = _fixture.CreateServiceProvider();
        var eventId = Guid.NewGuid();

        var command = TelemetryTestHelpers.CreateCommand(
            eventId,
            "SHP-DUP",
            "CONT-DUP",
            TelemetryEventType.LocationReported,
            1);

        var first = await TelemetryTestHelpers.SendAsync(serviceProvider, command);
        var second = await TelemetryTestHelpers.SendAsync(serviceProvider, command);

        first.Outcome.Should().Be(TelemetryProcessingOutcome.Accepted);
        second.Outcome.Should().Be(TelemetryProcessingOutcome.Duplicate);

        var state = await TelemetryTestHelpers.GetStateAsync(serviceProvider, "SHP-DUP");
        state!.LastAcceptedSequence.Should().Be(1);
        state.Version.Should().Be(1);

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShipmentTelemetryDbContext>();
        var processedCount = await dbContext.ProcessedTelemetry
            .CountAsync(x => x.EventId == eventId);
        processedCount.Should().Be(1);

        var outboxCount = await dbContext.OutboxMessages.CountAsync();
        outboxCount.Should().Be(1);
    }

    [Fact]
    public async Task SameEventIdWithDifferentPayload_IsRejected()
    {
        await _fixture.ResetDatabaseAsync();
        var serviceProvider = _fixture.CreateServiceProvider();
        var eventId = Guid.NewGuid();

        var first = await TelemetryTestHelpers.SendAsync(
            serviceProvider,
            TelemetryTestHelpers.CreateCommand(
                eventId,
                "SHP-PAYLOAD",
                "CONT-PAYLOAD",
                TelemetryEventType.LocationReported,
                1,
                """{"source":"first"}"""));

        var second = await TelemetryTestHelpers.SendAsync(
            serviceProvider,
            TelemetryTestHelpers.CreateCommand(
                eventId,
                "SHP-PAYLOAD",
                "CONT-PAYLOAD",
                TelemetryEventType.LocationReported,
                1,
                """{"source":"second"}"""));

        first.Outcome.Should().Be(TelemetryProcessingOutcome.Accepted);
        second.Outcome.Should().Be(TelemetryProcessingOutcome.PayloadConflict);

        var state = await TelemetryTestHelpers.GetStateAsync(serviceProvider, "SHP-PAYLOAD");
        state!.LastAcceptedSequence.Should().Be(1);

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShipmentTelemetryDbContext>();
        (await dbContext.OutboxMessages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task StaleSequence_DoesNotOverwriteNewerState()
    {
        await _fixture.ResetDatabaseAsync();
        var serviceProvider = _fixture.CreateServiceProvider();

        await SeedOperationalStateAsync(
            serviceProvider,
            "SHP-STALE",
            "CONT-STALE",
            OperationalMilestone.GateIn,
            lastAcceptedSequence: 105,
            version: 2);

        var staleResult = await TelemetryTestHelpers.SendAsync(
            serviceProvider,
            TelemetryTestHelpers.CreateCommand(
                Guid.NewGuid(),
                "SHP-STALE",
                "CONT-STALE",
                TelemetryEventType.LoadConfirmed,
                104));

        staleResult.Outcome.Should().Be(TelemetryProcessingOutcome.Stale);

        var state = await TelemetryTestHelpers.GetStateAsync(serviceProvider, "SHP-STALE");
        state!.CurrentMilestone.Should().Be((int)OperationalMilestone.GateIn);
        state.LastAcceptedSequence.Should().Be(105);
        state.Version.Should().Be(2);
    }

    [Fact]
    public async Task InvalidMilestoneTransition_DoesNotCorruptState()
    {
        await _fixture.ResetDatabaseAsync();
        var serviceProvider = _fixture.CreateServiceProvider();

        await SeedOperationalStateAsync(
            serviceProvider,
            "SHP-INVALID",
            "CONT-INVALID",
            OperationalMilestone.GateIn,
            lastAcceptedSequence: 2,
            version: 2);

        var result = await TelemetryTestHelpers.SendAsync(
            serviceProvider,
            TelemetryTestHelpers.CreateCommand(
                Guid.NewGuid(),
                "SHP-INVALID",
                "CONT-INVALID",
                TelemetryEventType.GateOutDetected,
                3));

        result.Outcome.Should().Be(TelemetryProcessingOutcome.InvalidTransition);

        var state = await TelemetryTestHelpers.GetStateAsync(serviceProvider, "SHP-INVALID");
        state!.CurrentMilestone.Should().Be((int)OperationalMilestone.GateIn);
        state.LastAcceptedSequence.Should().Be(2);
    }

    [Fact]
    public async Task ConcurrentEvents_ProduceValidDeterministicState()
    {
        await _fixture.ResetDatabaseAsync();
        var serviceProvider = _fixture.CreateServiceProvider();

        await TelemetryTestHelpers.SendAsync(
            serviceProvider,
            TelemetryTestHelpers.CreateCommand(
                Guid.NewGuid(),
                "SHP-CONC",
                "CONT-CONC",
                TelemetryEventType.LocationReported,
                200));

        var gateInCommand = TelemetryTestHelpers.CreateCommand(
            Guid.NewGuid(),
            "SHP-CONC",
            "CONT-CONC",
            TelemetryEventType.GateInDetected,
            201);

        var loadCommand = TelemetryTestHelpers.CreateCommand(
            Guid.NewGuid(),
            "SHP-CONC",
            "CONT-CONC",
            TelemetryEventType.LoadConfirmed,
            202);

        using var barrier = new Barrier(2);

        var tasks = Enumerable.Range(0, 2).Select(index => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await using var scope = serviceProvider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            return await mediator.Send(index == 0 ? gateInCommand : loadCommand);
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().Contain(r => r.Outcome == TelemetryProcessingOutcome.Accepted);
        results.Count(r => r.Outcome == TelemetryProcessingOutcome.Accepted).Should().Be(2);

        var state = await TelemetryTestHelpers.GetStateAsync(serviceProvider, "SHP-CONC");
        state!.LastAcceptedSequence.Should().Be(202);
        state.CurrentMilestone.Should().Be((int)OperationalMilestone.LoadedOnVessel);
        state.Version.Should().Be(3);

        await using var verifyScope = serviceProvider.CreateAsyncScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<ShipmentTelemetryDbContext>();
        (await dbContext.OutboxMessages.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task AcceptedMilestone_PersistsPendingOutboxMessage()
    {
        await _fixture.ResetDatabaseAsync();
        var serviceProvider = _fixture.CreateServiceProvider();

        var result = await TelemetryTestHelpers.SendAsync(
            serviceProvider,
            TelemetryTestHelpers.CreateCommand(
                Guid.NewGuid(),
                "SHP-OUTBOX",
                "CONT-OUTBOX",
                TelemetryEventType.LocationReported,
                1));

        result.Outcome.Should().Be(TelemetryProcessingOutcome.Accepted);

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShipmentTelemetryDbContext>();

        var pendingOutbox = await dbContext.OutboxMessages
            .Where(x => x.Status == (int)OutboxMessageStatus.Pending)
            .ToListAsync();

        pendingOutbox.Should().HaveCount(1);
        pendingOutbox[0].MessageType.Should().Be(nameof(ShipmentMilestoneRecordedIntegrationEvent));

        var state = await dbContext.ShipmentOperationalStates
            .AsNoTracking()
            .SingleAsync(x => x.ShipmentId == "SHP-OUTBOX");
        state.CurrentMilestone.Should().Be((int)OperationalMilestone.ArrivedAtPort);
    }

    [Fact]
    public async Task DuplicateIntegrationEventDelivery_ProducesOneDownstreamEffect()
    {
        await _fixture.ResetDatabaseAsync();
        var serviceProvider = _fixture.CreateServiceProvider();
        var eventId = Guid.NewGuid();

        var integrationEvent = new ShipmentMilestoneRecordedIntegrationEvent(
            eventId,
            "SHP-DOWN",
            "CONT-DOWN",
            OperationalMilestone.GateIn.ToString(),
            10,
            DateTimeOffset.UtcNow);

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var consumer = scope.ServiceProvider.GetRequiredService<ShipmentMilestoneRecordedConsumer>();
            await consumer.HandleAsync(integrationEvent, CancellationToken.None);
            await consumer.HandleAsync(integrationEvent, CancellationToken.None);
        }

        await using var verifyScope = serviceProvider.CreateAsyncScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<ShipmentTelemetryDbContext>();

        (await dbContext.ProcessedIntegrationMessages.CountAsync()).Should().Be(1);
        (await dbContext.DownstreamMilestoneNotifications.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ReplayOfProcessedTelemetry_DoesNotDuplicateMilestones()
    {
        await _fixture.ResetDatabaseAsync();
        var serviceProvider = _fixture.CreateServiceProvider();

        var commands = new[]
        {
            TelemetryTestHelpers.CreateCommand(Guid.NewGuid(), "SHP-REPLAY", "CONT-REPLAY", TelemetryEventType.LocationReported, 1),
            TelemetryTestHelpers.CreateCommand(Guid.NewGuid(), "SHP-REPLAY", "CONT-REPLAY", TelemetryEventType.GateInDetected, 2),
            TelemetryTestHelpers.CreateCommand(Guid.NewGuid(), "SHP-REPLAY", "CONT-REPLAY", TelemetryEventType.LoadConfirmed, 3)
        };

        foreach (var command in commands)
        {
            (await TelemetryTestHelpers.SendAsync(serviceProvider, command)).Outcome
                .Should().Be(TelemetryProcessingOutcome.Accepted);
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShipmentTelemetryDbContext>();
        var outboxBeforeReplay = await dbContext.OutboxMessages.CountAsync();
        var versionBeforeReplay = (await dbContext.ShipmentOperationalStates
            .AsNoTracking()
            .SingleAsync(x => x.ShipmentId == "SHP-REPLAY")).Version;

        foreach (var command in commands)
        {
            (await TelemetryTestHelpers.SendAsync(serviceProvider, command)).Outcome
                .Should().Be(TelemetryProcessingOutcome.Duplicate);
        }

        var state = await TelemetryTestHelpers.GetStateAsync(serviceProvider, "SHP-REPLAY");
        state!.LastAcceptedSequence.Should().Be(3);
        state.CurrentMilestone.Should().Be((int)OperationalMilestone.LoadedOnVessel);
        state.Version.Should().Be(versionBeforeReplay);

        (await dbContext.OutboxMessages.CountAsync()).Should().Be(outboxBeforeReplay);
    }

    private static async Task SeedOperationalStateAsync(
        IServiceProvider serviceProvider,
        string shipmentId,
        string containerId,
        OperationalMilestone milestone,
        long lastAcceptedSequence,
        uint version)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShipmentTelemetryDbContext>();

        dbContext.ShipmentOperationalStates.Add(new ShipmentOperationalStateEntity
        {
            ShipmentId = shipmentId,
            ContainerId = containerId,
            CurrentMilestone = (int)milestone,
            LastAcceptedSequence = lastAcceptedSequence,
            Version = version,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        dbContext.ShipmentOperationalReadModels.Add(new ShipmentOperationalReadModelEntity
        {
            ShipmentId = shipmentId,
            ContainerId = containerId,
            CurrentMilestone = (int)milestone,
            LastAcceptedSequence = lastAcceptedSequence,
            Version = version,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }
}
