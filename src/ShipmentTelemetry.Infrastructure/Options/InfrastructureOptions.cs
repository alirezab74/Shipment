namespace ShipmentTelemetry.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;
}

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; set; } = true;

    public string ConnectionString { get; set; } = "localhost:6379";
}

public sealed class CapBrokerOptions
{
    public const string SectionName = "Cap";

    public bool Enabled { get; set; } = true;

    public string Transport { get; set; } = "RabbitMQ";

    public string DefaultGroupName { get; set; } = "shipment-telemetry";

    public int FailedRetryCount { get; set; } = 3;

    public int FailedRetryIntervalSeconds { get; set; } = 60;

    public string RabbitMqHost { get; set; } = "localhost";

    public int RabbitMqPort { get; set; } = 5672;

    public string RabbitMqUsername { get; set; } = "guest";

    public string RabbitMqPassword { get; set; } = "guest";

    public string RabbitMqVirtualHost { get; set; } = "/";

    public string RabbitMqExchangeName { get; set; } = "shipment.telemetry";

    public string KafkaBootstrapServers { get; set; } = "localhost:9092";
}

public sealed class TelemetryRetentionOptions
{
    public const string SectionName = "TelemetryRetention";

    public bool Enabled { get; set; } = true;

    public int RetentionDays { get; set; } = 30;

    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(6);
}
