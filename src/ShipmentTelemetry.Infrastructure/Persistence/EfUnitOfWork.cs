using ShipmentTelemetry.Application.Abstractions;

namespace ShipmentTelemetry.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly ShipmentTelemetryDbContext _dbContext;

    public EfUnitOfWork(ShipmentTelemetryDbContext dbContext) => _dbContext = dbContext;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
