using NetArchTest.Rules;
using TaskManager.Notifications.Application;

namespace TaskManager.Notifications.Tests.Architecture;

public class OnionDependencyRuleTests
{
    private static readonly System.Reflection.Assembly Asm = typeof(EventMapper).Assembly;

    [Fact]
    public void Application_does_not_reference_Infrastructure_Presentation_or_adapter_packages()
    {
        var forbidden = new[]
        {
            "TaskManager.Notifications.Infrastructure",
            "TaskManager.Notifications.Presentation",
            "StackExchange.Redis",
            "MassTransit",
            "MailKit",
            "Microsoft.AspNetCore.SignalR",
        };

        var result = Types.InAssembly(Asm)
            .That().ResideInNamespaceStartingWith("TaskManager.Notifications.Application")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildMessage(result));
    }

    [Fact]
    public void Infrastructure_is_not_referenced_by_Application()
    {
        var result = Types.InAssembly(Asm)
            .That().ResideInNamespaceStartingWith("TaskManager.Notifications.Application")
            .ShouldNot().HaveDependencyOnAny("TaskManager.Notifications.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildMessage(result));
    }

    private static string BuildMessage(TestResult result)
        => result.IsSuccessful
            ? string.Empty
            : "Violations: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());
}
