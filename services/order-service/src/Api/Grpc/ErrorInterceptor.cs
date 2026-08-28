using Ecommerce.OrderService.Domain.Abstractions;
using FluentValidation;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.OrderService.Api.Grpc;

/// Translates exceptions into gRPC statuses in one place, so no handler has to know what a
/// status code is. An untranslated exception reaches the caller as UNKNOWN, which says
/// only "something broke here" and is never the right answer.
public sealed class ErrorInterceptor(ILogger<ErrorInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (ValidationException exception)
        {
            // The request never satisfied its own shape, so the domain was never asked.
            var detail = string.Join("; ", exception.Errors.Select(e => e.ErrorMessage));
            throw new RpcException(new Status(StatusCode.InvalidArgument, detail));
        }
        catch (DomainException exception)
        {
            // The request was well formed and the domain refused it.
            throw new RpcException(new Status(StatusCode.FailedPrecondition, exception.Message));
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else wrote this order between our read and our write. ABORTED is the
            // status a client is expected to retry, which is exactly the right advice here.
            logger.LogWarning("Concurrent update rejected for {Method}", context.Method);
            throw new RpcException(new Status(
                StatusCode.Aborted, "This order changed while you were editing it. Read it again and retry."));
        }
    }
}
