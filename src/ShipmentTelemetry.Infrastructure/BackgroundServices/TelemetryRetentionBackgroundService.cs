using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShipmentTelemetry.Infrastructure.Options;
using ShipmentTelemetry.Infrastructure.Persistence;

namespace ShipmentTelemetry.Infrastructure.BackgroundServices;

public sealed class TelemetryRetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetryRetentionBackgroundService> _logger;
    private readonly TelemetryRetentionOptions _options;

    public TelemetryRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetryRetentionBackgroundService> logger,
        IOptions<TelemetryRetentionOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telemetry retention cleanup failed.");
            }

            await Task.Delay(_options.CleanupInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShipmentTelemetryDbContext>();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-_options.RetentionDays);

        var deleted = await dbContext.ProcessedTelemetry
            .Where(x => x.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Telemetry retention removed {Count} processed telemetry records older than {Cutoff}.",
                deleted,
                cutoff);
        }
    }
}
