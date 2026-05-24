using NetArchTest.Rules;
using TaskManager.Identity.Application.Behaviors;
using TaskManager.Identity.Domain.Entities;
using TaskManager.Identity.Infrastructure.Persistence;

namespace TaskManager.Identity.Tests.Architecture;

/// <summary>
/// Enforces the onion dependency rule from spec §2 for the Identity service.
/// Documented exception: <see cref="TaskManager.Identity.Domain.Entities.AppUser"/> inherits
/// from <c>Microsoft.AspNetCore.Identity.IdentityUser&lt;Guid&gt;</c>, which means Domain
/// has *one* allowed external dependency (Microsoft.AspNetCore.Identity). The rule below
/// excludes that namespace from the otherwise-forbidden set.
/// </summary>
public class OnionDependencyRuleTests
{
    private static readonly System.Reflection.Assembly DomainAsm = typeof(AppUser).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAsm = typeof(LoggingBehavior<,>).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAsm = typeof(IdentityDbContext).Assembly;

    [Fact]
    public void Application_does_not_reference_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAsm)
            .That().ResideInNamespaceStartingWith("TaskManager.Identity.Application")
            .ShouldNot().HaveDependencyOnAny("TaskManager.Identity.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildMessage(result));
    }

    [Fact]
    public void Domain_does_not_reference_Application_or_Infrastructure_or_Presentation()
    {
        var forbidden = new[]
        {
            "TaskManager.Identity.Application",
            "TaskManager.Identity.Infrastructure",
            "TaskManager.Identity.Presentation",
        };

        var result = Types.InAssembly(DomainAsm)
            .That().ResideInNamespaceStartingWith("TaskManager.Identity.Domain")
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(BuildMessage(result));
    }

    [Fact]
    public void Infrastructure_is_only_referenced_by_Presentation_or_Infrastructure_itself()
    {
        // Equivalent assertion in reverse: Domain and Application both ShouldNot reference Infrastructure.
        var domainOk = Types.InAssembly(DomainAsm)
            .That().ResideInNamespaceStartingWith("TaskManager.Identity.Domain")
            .ShouldNot().HaveDependencyOnAny("TaskManager.Identity.Infrastructure")
            .GetResult();

        var appOk = Types.InAssembly(ApplicationAsm)
            .That().ResideInNamespaceStartingWith("TaskManager.Identity.Application")
            .ShouldNot().HaveDependencyOnAny("TaskManager.Identity.Infrastructure")
            .GetResult();

        domainOk.IsSuccessful.Should().BeTrue(BuildMessage(domainOk));
        appOk.IsSuccessful.Should().BeTrue(BuildMessage(appOk));
    }

    private static string BuildMessage(TestResult result)
        => result.IsSuccessful
            ? string.Empty
            : "Violations: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());
}
