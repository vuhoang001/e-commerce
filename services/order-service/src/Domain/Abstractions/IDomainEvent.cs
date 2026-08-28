namespace Ecommerce.OrderService.Domain.Abstractions;

/// Something that has already happened inside this process.
///
/// Not to be confused with an integration event in `proto/events/`. A domain event is
/// in-process and may reference domain types; an integration event crosses the network,
/// is defined in the contract, and is a separate type. Publishing one of these to Kafka
/// is a mistake — see the ddd-dotnet skill.
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
