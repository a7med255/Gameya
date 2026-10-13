using Gameya.Application.Abstractions;
using Gameya.Domain.Entities;
using Gameya.Infrastructure.Persistence;
using NetArchTest.Rules;

namespace Gameya.Tests.Architecture;

public sealed class DependencyTests
{
    [Fact]
    public void Domain_has_no_outward_layer_dependencies()
    {
        var result = Types.InAssembly(typeof(Association).Assembly).ShouldNot()
            .HaveDependencyOnAny("Gameya.Application", "Gameya.Infrastructure", "Gameya.API",
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore.Identity").GetResult();
        Assert.True(result.IsSuccessful, string.Join(Environment.NewLine, result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_api()
    {
        var result = Types.InAssembly(typeof(IApplicationDbContext).Assembly).ShouldNot()
            .HaveDependencyOnAny("Gameya.Infrastructure", "Gameya.API").GetResult();
        Assert.True(result.IsSuccessful, string.Join(Environment.NewLine, result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Infrastructure_is_the_persistence_boundary()
    {
        Assert.Equal("Gameya.Infrastructure", typeof(ApplicationDbContext).Assembly.GetName().Name);
    }
}
