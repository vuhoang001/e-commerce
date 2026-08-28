namespace Ecommerce.OrderService.Domain.Abstractions;

/// Declared here, implemented in Infrastructure. The domain states that it needs changes
/// committed atomically; it says nothing about how, and knows nothing about EF Core.
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
