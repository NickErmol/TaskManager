using TaskManager.Identity.Application.Commands;
using TaskManager.Identity.Application.Validators;

namespace TaskManager.Identity.Tests.Unit;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _sut = new();

    [Theory]
    [InlineData("not-an-email", "Alice", "Password1", false, "email format")]
    [InlineData("alice@example.com", "A", "Password1", false, "display name too short")]
    [InlineData("alice@example.com", "Alice", "short", false, "password too short")]
    [InlineData("alice@example.com", "Alice", "password1", false, "password missing uppercase")]
    [InlineData("alice@example.com", "Alice", "Password", false, "password missing digit")]
    [InlineData("alice@example.com", "Alice", "Password1", true, "valid")]
    public void Validate(string email, string name, string password, bool expected, string scenario)
    {
        var result = _sut.Validate(new RegisterCommand(email, name, password));
        result.IsValid.Should().Be(expected, scenario);
    }
}
