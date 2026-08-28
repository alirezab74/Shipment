using System.Text.Json;
using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShipmentTelemetry.Application.Abstractions;
using ShipmentTelemetry.Contracts.IntegrationEvents;
using ShipmentTelemetry.Infrastructure.Observability;
using ShipmentTelemetry.Infrastructure.Persistence.Entities;
using ShipmentTelemetry.Infrastructure.Persistence;

namespace ShipmentTelemetry.Infrastructure.Messaging;

public sealed class OutboxPublisherBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger;
    private readonly OutboxPublisherOptions _options;

    public OutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherBackgroundService> logger,
        IOptions<OutboxPublisherOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox publisher loop failed.");
            }

            await Task.Delay(_options.PollingInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShipmentTelemetryDbContext>();
        var capPublisher = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
        var metrics = scope.ServiceProvider.GetRequiredService<ITelemetryMetrics>();

        var pending = await dbContext.OutboxMessages
            .Where(x => x.Status == (int)OutboxMessageStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var backlog = await dbContext.OutboxMessages
            .CountAsync(x => x.Status == (int)OutboxMessageStatus.Pending, cancellationToken)
            .ConfigureAwait(false);
        PrometheusTelemetryMetrics.SetOutboxBacklog(backlog);

        foreach (var message in pending)
        {
            try
            {
                if (message.MessageType == nameof(ShipmentMilestoneRecordedIntegrationEvent))
                {
                    var integrationEvent = JsonSerializer.Deserialize<ShipmentMilestoneRecordedIntegrationEvent>(
                        message.PayloadJson)!;

                    await capPublisher.PublishAsync(
                        nameof(ShipmentMilestoneRecordedIntegrationEvent),
                        integrationEvent,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                message.Status = (int)OutboxMessageStatus.Published;
                message.PublishedAt = DateTimeOffset.UtcNow;
                message.LastError = null;

                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                metrics.OutboxPublished();

                _logger.LogInformation(
                    "Outbox message published. OutboxId={OutboxId} MessageType={MessageType}",
                    message.Id,
                    message.MessageType);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.LastError = ex.Message;

                if (message.RetryCount >= _options.MaxRetries)
                {
                    message.Status = (int)OutboxMessageStatus.Quarantined;
                    _logger.LogError(
                        ex,
                        "Outbox message quarantined after retries. OutboxId={OutboxId}",
                        message.Id);
                }

                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

public sealed class OutboxPublisherOptions
{
    public const string SectionName = "OutboxPublisher";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    public int BatchSize { get; set; } = 50;

    public int MaxRetries { get; set; } = 5;
}
