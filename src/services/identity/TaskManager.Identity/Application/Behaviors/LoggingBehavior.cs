using System.Diagnostics;
using FluentResults;
using Mediator;
using Microsoft.Extensions.Logging;

namespace TaskManager.Identity.Application.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IMessage
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async ValueTask<TResponse> Handle(
        TRequest message,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("Dispatching {RequestName}", requestName);
        var response = await next(message, cancellationToken);
        sw.Stop();

        // If response is a Result, log success/failure based on its state.
        if (response is IResultBase result)
        {
            if (result.IsSuccess)
                _logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms (success)", requestName, sw.ElapsedMilliseconds);
            else
                _logger.LogWarning("Handled {RequestName} in {ElapsedMs}ms with errors: {Errors}",
                    requestName, sw.ElapsedMilliseconds, string.Join("; ", result.Errors.Select(e => e.Message)));
        }
        else
        {
            _logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms", requestName, sw.ElapsedMilliseconds);
        }

        return response;
    }
}
