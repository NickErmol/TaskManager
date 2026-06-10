using NetArchTest.Rules;

namespace TaskManager.Tasks.Tests.Architecture;

public class OnionDependencyRuleTests
{
    private static readonly System.Reflection.Assembly Asm = typeof(TaskItem).Assembly;

    [Fact]
    public void Domain_does_not_reference_outer_layers_or_infrastructure_packages()
    {
        var forbidden = new[]
        {
            "TaskManager.Tasks.Application",
            "TaskManager.Tasks.Infrastructure",
            "TaskManager.Tasks.Presentation",
            "Microsoft.EntityFrameworkCore",
            "MassTransit",
        };

        var result = Types.InAssembly(Asm)
            .That().ResideInNamespaceStartingWith("TaskManager.Tasks.Domain")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildMessage(result));
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure_or_Presentation()
    {
        var forbidden = new[]
        {
            "TaskManager.Tasks.Infrastructure",
            "TaskManager.Tasks.Presentation",
            "Microsoft.EntityFrameworkCore",
        };

        var result = Types.InAssembly(Asm)
            .That().ResideInNamespaceStartingWith("TaskManager.Tasks.Application")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildMessage(result));
    }

    private static string BuildMessage(TestResult result)
        => result.IsSuccessful
            ? string.Empty
            : "Violations: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());
}
