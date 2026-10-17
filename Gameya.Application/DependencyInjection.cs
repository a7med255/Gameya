using FluentValidation;
using Gameya.Application.Abstractions;
using Gameya.Application.Behaviors;
using Gameya.Application.Features.Finance;
using Gameya.Application.Features.Payments;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Gameya.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<IMonthlyFinanceService, MonthlyFinanceService>();
        services.AddScoped<PaymentAllocationService>();
        return services;
    }
}
