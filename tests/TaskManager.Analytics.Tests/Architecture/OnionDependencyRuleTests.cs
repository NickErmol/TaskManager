using NetArchTest.Rules;
using TaskManager.Analytics.Domain.ReadModels;

namespace TaskManager.Analytics.Tests.Architecture;

public class OnionDependencyRuleTests
{
    private static readonly System.Reflection.Assembly Asm = typeof(BoardStats).Assembly;

    [Fact]
    public void Domain_does_not_reference_outer_layers_or_infrastructure_packages()
    {
        var forbidden = new[]
        {
            "TaskManager.Analytics.Application",
            "TaskManager.Analytics.Infrastructure",
            "TaskManager.Analytics.Presentation",
            "Microsoft.EntityFrameworkCore",
            "MassTransit",
        };

        var result = Types.InAssembly(Asm)
            .That().ResideInNamespaceStartingWith("TaskManager.Analytics.Domain")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildMessage(result));
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure_or_Presentation()
    {
        var forbidden = new[]
        {
            "TaskManager.Analytics.Infrastructure",
            "TaskManager.Analytics.Presentation",
            "Microsoft.EntityFrameworkCore",
            "MassTransit",
        };

        var result = Types.InAssembly(Asm)
            .That().ResideInNamespaceStartingWith("TaskManager.Analytics.Application")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildMessage(result));
    }

    private static string BuildMessage(TestResult result)
        => result.IsSuccessful
            ? string.Empty
            : "Violations: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());
}
