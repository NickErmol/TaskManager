using FluentResults;
using FluentValidation;
using Mediator;

namespace TaskManager.Tasks.Application.Behaviors;

/// <summary>
/// Pipeline behavior that runs every FluentValidation validator registered for the request.
/// Validation failures surface as <see cref="Result.Fail"/> on the response — they never throw.
/// Requires <typeparamref name="TResponse"/> to be a <see cref="Result"/> or
/// <see cref="Result{T}"/>; non-Result responses are not validated by this pipeline.
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IMessage
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async ValueTask<TResponse> Handle(
        TRequest message,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next(message, cancellationToken);

        var ctx = new ValidationContext<TRequest>(message);
        var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(ctx, cancellationToken)));
        var errors = results.SelectMany(r => r.Errors).Where(e => e is not null).ToList();

        if (errors.Count == 0)
            return await next(message, cancellationToken);

        // TResponse is expected to be Result or Result<T>. Build a failure with all errors.
        var responseType = typeof(TResponse);
        if (responseType == typeof(Result))
        {
            object fail = Result.Fail(errors.Select(e => new Error(e.ErrorMessage)));
            return (TResponse)fail;
        }

        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var innerType = responseType.GenericTypeArguments[0];
            var failMethod = typeof(Result)
                .GetMethods()
                .First(m => m.Name == nameof(Result.Fail)
                            && m.IsGenericMethodDefinition
                            && m.GetParameters().Length == 1
                            && m.GetParameters()[0].ParameterType == typeof(IEnumerable<IError>))
                .MakeGenericMethod(innerType);

            var fail = failMethod.Invoke(null, new object[] { errors.Select(e => (IError)new Error(e.ErrorMessage)) })!;
            return (TResponse)fail;
        }

        // Response is not a Result — let it through. Validation has no opinion.
        return await next(message, cancellationToken);
    }
}
