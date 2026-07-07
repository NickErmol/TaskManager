using FluentResults;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using TaskManager.Identity.Application.Commands;
using TaskManager.Identity.Application.Mappers;
using TaskManager.Identity.Domain.Entities;
using TaskManager.Identity.Domain.Interfaces;

namespace TaskManager.Identity.Tests.Unit;

public class ExternalLoginCommandHandlerTests
{
    private readonly UserManager<AppUser> _userManager = BuildUserManagerStub();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IRefreshTokenRepository _refreshRepo = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IdentityMapper _mapper = new();

    public ExternalLoginCommandHandlerTests()
    {
        _tokens.IssueAccessToken(Arg.Any<AppUser>())
            .Returns(new AccessToken("access", DateTimeOffset.UtcNow.AddMinutes(15)));
        _tokens.IssueRefreshToken()
            .Returns(new RefreshTokenPair("plain", "hash", DateTimeOffset.UtcNow.AddDays(7)));
    }

    private ExternalLoginCommandHandler BuildSut()
        => new(_userManager, _tokens, _refreshRepo, _uow, _mapper);

    private static ExternalLoginCommand Cmd(
        string? email = "user@example.com", bool verified = true, string? name = "User")
        => new("fake", "provider-key-1", email, verified, name);

    [Fact]
    public async Task Existing_external_login_signs_in_without_creating_or_linking()
    {
        var user = AppUser.Create("user@example.com", "User");
        _userManager.FindByLoginAsync("fake", "provider-key-1").Returns(user);

        var result = await BuildSut().Handle(Cmd(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<AppUser>());
        await _userManager.DidNotReceive().AddLoginAsync(Arg.Any<AppUser>(), Arg.Any<UserLoginInfo>());
    }

    [Fact]
    public async Task Verified_email_matching_existing_account_auto_links()
    {
        var existing = AppUser.Create("user@example.com", "User");
        _userManager.FindByLoginAsync("fake", "provider-key-1").ReturnsNull();
        _userManager.FindByEmailAsync("user@example.com").Returns(existing);
        _userManager.AddLoginAsync(existing, Arg.Any<UserLoginInfo>()).Returns(IdentityResult.Success);

        var result = await BuildSut().Handle(Cmd(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<AppUser>());
        await _userManager.Received(1).AddLoginAsync(existing,
            Arg.Is<UserLoginInfo>(l => l.LoginProvider == "fake" && l.ProviderKey == "provider-key-1"));
    }

    [Fact]
    public async Task New_verified_email_creates_confirmed_passwordless_user_and_links()
    {
        _userManager.FindByLoginAsync("fake", "provider-key-1").ReturnsNull();
        _userManager.FindByEmailAsync("user@example.com").ReturnsNull();
        _userManager.CreateAsync(Arg.Any<AppUser>()).Returns(IdentityResult.Success);
        _userManager.AddLoginAsync(Arg.Any<AppUser>(), Arg.Any<UserLoginInfo>()).Returns(IdentityResult.Success);

        var result = await BuildSut().Handle(Cmd(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _userManager.Received(1).CreateAsync(
            Arg.Is<AppUser>(u => u.Email == "user@example.com" && u.EmailConfirmed && u.DisplayName == "User"));
        await _userManager.Received(1).AddLoginAsync(Arg.Any<AppUser>(), Arg.Any<UserLoginInfo>());
    }

    [Fact]
    public async Task Missing_display_name_falls_back_to_email()
    {
        _userManager.FindByLoginAsync("fake", "provider-key-1").ReturnsNull();
        _userManager.FindByEmailAsync("user@example.com").ReturnsNull();
        _userManager.CreateAsync(Arg.Any<AppUser>()).Returns(IdentityResult.Success);
        _userManager.AddLoginAsync(Arg.Any<AppUser>(), Arg.Any<UserLoginInfo>()).Returns(IdentityResult.Success);

        await BuildSut().Handle(Cmd(name: null), CancellationToken.None);

        await _userManager.Received(1).CreateAsync(Arg.Is<AppUser>(u => u.DisplayName == "user@example.com"));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("user@example.com", false)]
    public async Task Unverified_or_missing_email_fails_without_touching_accounts(string? email, bool verified)
    {
        _userManager.FindByLoginAsync("fake", "provider-key-1").ReturnsNull();

        var result = await BuildSut().Handle(Cmd(email, verified), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("email-unverified"));
        await _userManager.DidNotReceive().FindByEmailAsync(Arg.Any<string>());
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<AppUser>());
    }

    private static UserManager<AppUser> BuildUserManagerStub()
    {
        var store = Substitute.For<IUserStore<AppUser>>();
        return Substitute.For<UserManager<AppUser>>(store, null!, null!, null!, null!, null!, null!, null!, null!);
    }
}
