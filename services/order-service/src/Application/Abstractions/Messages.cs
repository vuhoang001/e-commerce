using MediatR;

namespace Ecommerce.OrderService.Application.Abstractions;

/// Marks a request that changes state. The transaction behaviour keys off this: a query
/// must never open a write transaction, and this is what tells the pipeline which is which.
public interface ICommandMarker;

public interface ICommand<TResponse> : IRequest<Result<TResponse>>, ICommandMarker;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;

public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
