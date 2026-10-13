using FluentValidation;
using Gameya.Application.Common;
using MediatR;

namespace Gameya.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any()) return await next().ConfigureAwait(false);
        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(x => x.ValidateAsync(context, cancellationToken))).ConfigureAwait(false);
        var errors = results.SelectMany(x => x.Errors).Where(x => x is not null)
            .GroupBy(x => x.PropertyName)
            .ToDictionary(x => x.Key, x => x.Select(e => e.ErrorCode).Distinct().ToArray());
        if (errors.Count != 0) throw new RequestValidationException(errors);
        return await next().ConfigureAwait(false);
    }
}
