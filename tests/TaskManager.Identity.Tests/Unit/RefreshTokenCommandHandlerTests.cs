using FluentResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using TaskManager.Identity.Application.Commands;
using TaskManager.Identity.Application.Mappers;
using TaskManager.Identity.Domain.Entities;
using TaskManager.Identity.Domain.Interfaces;

namespace TaskManager.Identity.Tests.Unit;

/// <summary>
/// Reuse-detection tests are the load-bearing ones — the rest of the auth flow is exercised
/// through the integration suite.
/// </summary>
public class RefreshTokenCommandHandlerTests
{
    private readonly UserManager<AppUser> _userManager = BuildUserManagerStub();
    private readonly IRefreshTokenRepository _refreshRepo = Substitute.For<IRefreshTokenRepository>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IdentityMapper _mapper = new();

    private RefreshTokenCommandHandler BuildSut() => new(_userManager, _refreshRepo, _tokens, _uow, _mapper, NullLogger<RefreshTokenCommandHandler>.Instance);

    [Fact]
    public async Task Handle_when_token_unknown_returns_unauthorized()
    {
        _tokens.HashToken("plaintext").Returns("hash");
        _refreshRepo.GetByHashAsync("hash", Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);

        var result = await BuildSut().Handle(new RefreshTokenCommand("plaintext"), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.StartsWith("unauthorized:"));
    }

    [Fact]
    public async Task Handle_when_token_already_revoked_revokes_all_user_tokens_and_returns_unauthorized()
    {
        // Reuse detection — OWASP guidance, spec §4.2 security pass
        var userId = Guid.NewGuid();
        var stored = RefreshToken.Create(userId, "hash", DateTimeOffset.UtcNow.AddDays(7));
        stored.Revoke();

        _tokens.HashToken("plaintext").Returns("hash");
        _refreshRepo.GetByHashAsync("hash", Arg.Any<CancellationToken>()).Returns(stored);

        var result = await BuildSut().Handle(new RefreshTokenCommand("plaintext"), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("reused"));
        await _refreshRepo.Received(1).RevokeAllForUserAsync(userId, Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_when_token_expired_returns_unauthorized_without_revoking_all()
    {
        var userId = Guid.NewGuid();
        var expired = RefreshToken.Create(userId, "hash", DateTimeOffset.UtcNow.AddSeconds(-1));

        _tokens.HashToken("plaintext").Returns("hash");
        _refreshRepo.GetByHashAsync("hash", Arg.Any<CancellationToken>()).Returns(expired);

        var result = await BuildSut().Handle(new RefreshTokenCommand("plaintext"), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("expired"));
        await _refreshRepo.DidNotReceive().RevokeAllForUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_with_missing_plaintext_returns_unauthorized()
    {
        var result = await BuildSut().Handle(new RefreshTokenCommand(""), CancellationToken.None);
        result.IsFailed.Should().BeTrue();
    }

    private static UserManager<AppUser> BuildUserManagerStub()
    {
        // UserManager has a deep dep graph; NSubstitute can stub it because the relevant
        // methods are virtual on the concrete UserManager.
        var store = Substitute.For<IUserStore<AppUser>>();
        return Substitute.For<UserManager<AppUser>>(store, null!, null!, null!, null!, null!, null!, null!, null!);
    }
}
