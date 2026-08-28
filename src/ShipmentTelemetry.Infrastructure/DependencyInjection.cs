using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipmentTelemetry.Application.Abstractions;
using ShipmentTelemetry.Domain.Repositories;
using ShipmentTelemetry.Infrastructure.BackgroundServices;
using ShipmentTelemetry.Infrastructure.Caching;
using ShipmentTelemetry.Infrastructure.Messaging;
using ShipmentTelemetry.Infrastructure.Observability;
using ShipmentTelemetry.Infrastructure.Options;
using ShipmentTelemetry.Infrastructure.Persistence;
using ShipmentTelemetry.Infrastructure.Persistence.Repositories;
using ShipmentTelemetry.Infrastructure.Security;
using StackExchange.Redis;

namespace ShipmentTelemetry.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<CapBrokerOptions>(configuration.GetSection(CapBrokerOptions.SectionName));
        services.Configure<OutboxPublisherOptions>(configuration.GetSection(OutboxPublisherOptions.SectionName));
        services.Configure<TelemetryRetentionOptions>(configuration.GetSection(TelemetryRetentionOptions.SectionName));

        var connectionString = configuration.GetConnectionString("PostgreSql")
            ?? configuration["Database:ConnectionString"]
            ?? throw new InvalidOperationException("PostgreSQL connection string is not configured.");

        services.AddDbContext<ShipmentTelemetryDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IShipmentOperationalStateRepository, ShipmentOperationalStateRepository>();
        services.AddScoped<IProcessedTelemetryStore, ProcessedTelemetryStore>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IOperationalStateReadModel, OperationalStateReadModel>();
        services.AddScoped<ITelemetryStatusStore, TelemetryStatusStore>();
        services.AddScoped<IPayloadHasher, Sha256PayloadHasher>();
        services.AddSingleton<ITelemetryMetrics, PrometheusTelemetryMetrics>();

        AddRedis(services, configuration);

        var capOptions = configuration.GetSection(CapBrokerOptions.SectionName).Get<CapBrokerOptions>()
            ?? new CapBrokerOptions();

        if (capOptions.Enabled)
        {
            AddCap(services, configuration, connectionString);
            services.AddHostedService<OutboxPublisherBackgroundService>();
        }

        services.AddScoped<ShipmentMilestoneRecordedConsumer>();

        services.AddHostedService<TelemetryRetentionBackgroundService>();

        return services;
    }

    private static void AddRedis(IServiceCollection services, IConfiguration configuration)
    {
        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

        if (!redisOptions.Enabled)
        {
            services.AddSingleton<IShipmentOperationalStateCache, NoOpOperationalStateCache>();
            return;
        }

        var redisConnection = redisOptions.ConnectionString;
        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddSingleton<IShipmentOperationalStateCache, NoOpOperationalStateCache>();
            return;
        }

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var config = ConfigurationOptions.Parse(redisConnection);
            config.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(config);
        });

        services.AddSingleton<IShipmentOperationalStateCache, RedisOperationalStateCache>();
    }

    private static void AddCap(
        IServiceCollection services,
        IConfiguration configuration,
        string postgresConnectionString)
    {
        var brokerOptions = configuration.GetSection(CapBrokerOptions.SectionName).Get<CapBrokerOptions>()
            ?? new CapBrokerOptions();

        services.AddCap(options =>
        {
            options.UsePostgreSql(postgresConnectionString);
            options.DefaultGroupName = brokerOptions.DefaultGroupName;
            options.FailedRetryCount = brokerOptions.FailedRetryCount;
            options.FailedRetryInterval = brokerOptions.FailedRetryIntervalSeconds;

            if (string.Equals(brokerOptions.Transport, "RabbitMQ", StringComparison.OrdinalIgnoreCase))
            {
                options.UseRabbitMQ(mq =>
                {
                    mq.HostName = brokerOptions.RabbitMqHost;
                    mq.Port = brokerOptions.RabbitMqPort;
                    mq.UserName = brokerOptions.RabbitMqUsername;
                    mq.Password = brokerOptions.RabbitMqPassword;
                    mq.VirtualHost = brokerOptions.RabbitMqVirtualHost;
                    mq.ExchangeName = brokerOptions.RabbitMqExchangeName;
                });
            }
            else if (string.Equals(brokerOptions.Transport, "Kafka", StringComparison.OrdinalIgnoreCase))
            {
                options.UseKafka(kafka =>
                {
                    kafka.Servers = brokerOptions.KafkaBootstrapServers;
                });
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported CAP transport '{brokerOptions.Transport}'. Use RabbitMQ or Kafka.");
            }
        });
    }
}
