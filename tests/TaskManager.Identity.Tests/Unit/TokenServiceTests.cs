using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Identity.Domain.Entities;
using TaskManager.Identity.Infrastructure.Services;

namespace TaskManager.Identity.Tests.Unit;

public class TokenServiceTests
{
    private const string Secret = "this-is-a-very-long-test-secret-key-for-hs256-signing-purposes-only";
    private static readonly JwtOptions Options = new()
    {
        Issuer = "TaskManager.Identity",
        Audience = "TaskManager",
        SecretKey = Secret,
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7,
    };

    private static TokenService BuildSut() => new(new OptionsWrapper<JwtOptions>(Options));

    [Fact]
    public void IssueAccessToken_includes_sub_email_name_jti_claims()
    {
        var user = AppUser.Create("alice@example.com", "Alice");
        var sut = BuildSut();

        var token = sut.IssueAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Value);

        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "alice@example.com");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Name && c.Value == "Alice");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
    }

    [Fact]
    public void IssueAccessToken_expires_in_15_minutes_signed_with_HS256()
    {
        var user = AppUser.Create("bob@example.com", "Bob");
        var sut = BuildSut();

        var token = sut.IssueAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Value);

        jwt.SignatureAlgorithm.Should().Be(SecurityAlgorithms.HmacSha256);
        token.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(10));
        jwt.Issuer.Should().Be("TaskManager.Identity");
        jwt.Audiences.Should().Contain("TaskManager");
    }

    [Fact]
    public void IssueRefreshToken_returns_unique_plaintext_and_matching_sha256_hash()
    {
        var sut = BuildSut();

        var a = sut.IssueRefreshToken();
        var b = sut.IssueRefreshToken();

        a.Plaintext.Should().NotBe(b.Plaintext, "two tokens must have unique plaintext");
        a.Plaintext.Length.Should().Be(128, "64 random bytes hex-encoded = 128 chars");
        a.Hash.Should().Be(sut.HashToken(a.Plaintext));
        a.Hash.Should().NotBe(a.Plaintext, "hash must differ from plaintext");
    }

    [Fact]
    public void HashToken_is_deterministic_sha256_hex()
    {
        var sut = BuildSut();

        var hash1 = sut.HashToken("known-input");
        var hash2 = sut.HashToken("known-input");
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("known-input")));

        hash1.Should().Be(hash2);
        hash1.Should().Be(expected);
    }

    [Fact]
    public void IssueAccessToken_throws_when_secret_missing()
    {
        var sut = new TokenService(new OptionsWrapper<JwtOptions>(new JwtOptions
        {
            Issuer = "x", Audience = "x", SecretKey = ""
        }));

        var act = () => sut.IssueAccessToken(AppUser.Create("z@z.com", "Z"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*JWT_SECRET*");
    }
}
