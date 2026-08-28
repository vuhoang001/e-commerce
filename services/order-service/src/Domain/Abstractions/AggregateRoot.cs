namespace Ecommerce.OrderService.Domain.Abstractions;

/// The only entry point into a cluster of objects that change together. Load one, change
/// one, save one — two aggregates modified in a single SaveChanges is a design error.
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id) : base(id) { }

    protected AggregateRoot() { }

    /// Read by the persistence layer after SaveChanges succeeds. Events are never
    /// dispatched mid-transaction: an event announcing a change that then rolls back is
    /// a lie that consumers cannot take back.
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
