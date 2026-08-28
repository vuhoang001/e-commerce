namespace Ecommerce.OrderService.Domain.Abstractions;

/// Has an identity that outlives its values. Two entities are the same entity when their
/// ids match, however much their contents differ.
public abstract class Entity<TId>
    where TId : notnull
{
    protected Entity(TId id) => Id = id;

    /// Only for the persistence layer's materialiser, which sets Id by other means.
    protected Entity() => Id = default!;

    public TId Id { get; private set; }

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other && other.GetType() == GetType() && Id.Equals(other.Id);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
