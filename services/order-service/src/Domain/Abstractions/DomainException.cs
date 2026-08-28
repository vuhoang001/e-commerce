namespace Ecommerce.OrderService.Domain.Abstractions;

/// A domain invariant was violated. Thrown, not returned: reaching this means the caller
/// asked for something the model does not permit, which is exceptional by definition.
/// Expected outcomes — an order that simply is not found — are a handler's result type,
/// not this.
public sealed class DomainException(string message) : Exception(message);
