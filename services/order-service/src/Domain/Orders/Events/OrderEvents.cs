using Ecommerce.OrderService.Domain.Abstractions;

namespace Ecommerce.OrderService.Domain.Orders.Events;

/// In-process domain events. Their counterparts in `proto/events/` are separate types with
/// separate lifecycles; these must never be published to Kafka directly. See the
/// ddd-dotnet skill.
public sealed record OrderPlaced(
    OrderId OrderId,
    CustomerId CustomerId,
    Money Total,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderConfirmed(
    OrderId OrderId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderCancelled(
    OrderId OrderId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
