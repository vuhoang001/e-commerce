using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecommerce.OrderService.Application.Behaviours;

/// First in the pipeline, so it sees everything the later stages do — including a
/// validation failure, which would otherwise be invisible from the handler's point of view.
public sealed class LoggingBehaviour<TRequest, TResponse>(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Handling {Request}", name);

        try
        {
            var response = await next();
            logger.LogInformation("Handled {Request} in {ElapsedMs} ms", name, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{Request} failed after {ElapsedMs} ms", name, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
