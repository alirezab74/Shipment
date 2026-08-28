namespace ShipmentTelemetry.Domain.Common;

public abstract class AggregateRoot
{
    public uint Version { get; protected set; }

    private readonly List<object> _domainEvents = [];

    public IReadOnlyList<object> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(object domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
