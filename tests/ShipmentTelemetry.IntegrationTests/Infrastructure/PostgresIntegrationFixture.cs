using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using ShipmentTelemetry.Application;
using ShipmentTelemetry.Application.Telemetry.Commands;
using ShipmentTelemetry.Domain.Enums;
using ShipmentTelemetry.Infrastructure;
using ShipmentTelemetry.Infrastructure.Messaging;
using ShipmentTelemetry.Infrastructure.Persistence;
using ShipmentTelemetry.Infrastructure.Persistence.Entities;
using Testcontainers.PostgreSql;

namespace ShipmentTelemetry.IntegrationTests.Infrastructure;

[CollectionDefinition(nameof(PostgresIntegrationCollection))]
public sealed class PostgresIntegrationCollection : ICollectionFixture<PostgresIntegrationFixture>;

public sealed class PostgresIntegrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        var options = new DbContextOptionsBuilder<ShipmentTelemetryDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var dbContext = new ShipmentTelemetryDbContext(options);
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    public IServiceProvider CreateServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = ConnectionString,
                ["Cap:Enabled"] = "false",
                ["Redis:Enabled"] = "false",
                ["TelemetryRetention:Enabled"] = "false"
            })
            .Build();

        var hostEnvironment = new HostingEnvironment
        {
            EnvironmentName = Environments.Development
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(hostEnvironment);
        services.AddApplication();
        services.AddInfrastructure(configuration, hostEnvironment);

        return services.BuildServiceProvider();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = CreateServiceProvider().CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShipmentTelemetryDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync("""
            TRUNCATE TABLE downstream_milestone_notifications,
                           processed_integration_messages,
                           outbox_messages,
                           telemetry_statuses,
                           processed_telemetry,
                           shipment_operational_read_models,
                           shipment_operational_states,
                           quarantined_telemetry
            RESTART IDENTITY CASCADE;
            """).ConfigureAwait(false);
    }
}

internal static class TelemetryTestHelpers
{
    public static ProcessTelemetryCommand CreateCommand(
        Guid eventId,
        string shipmentId,
        string containerId,
        TelemetryEventType eventType,
        long sequenceNumber,
        string payloadJson = """{"source":"test"}""")
        => new(
            eventId,
            containerId,
            shipmentId,
            eventType,
            sequenceNumber,
            DateTimeOffset.UtcNow,
            "DEV-TEST",
            "Test Port",
            10.0,
            20.0,
            payloadJson);

    public static async Task<ProcessTelemetryResult> SendAsync(
        IServiceProvider serviceProvider,
        ProcessTelemetryCommand command)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        return await mediator.Send(command).ConfigureAwait(false);
    }

    public static async Task<ShipmentOperationalStateEntity?> GetStateAsync(
        IServiceProvider serviceProvider,
        string shipmentId)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShipmentTelemetryDbContext>();
        return await dbContext.ShipmentOperationalStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ShipmentId == shipmentId)
            .ConfigureAwait(false);
    }

    public static async Task<int> CountOutboxAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShipmentTelemetryDbContext>();
        return await dbContext.OutboxMessages.CountAsync(x => x.Status == (int)OutboxMessageStatus.Pending)
            .ConfigureAwait(false);
    }

    public static async Task<int> CountDownstreamNotificationsAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShipmentTelemetryDbContext>();
        return await dbContext.DownstreamMilestoneNotifications.CountAsync().ConfigureAwait(false);
    }
}
