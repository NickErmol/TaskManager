using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TaskManager.Gateway.Tests.Integration;

/// <summary>
/// Mints HS256 tokens with the same shape the Identity service issues
/// (issuer TaskManager.Identity, audience TaskManager, sub/email/jti claims).
/// </summary>
public static class TestJwt
{
    public static string Issue(Guid userId, string email, string? secret = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret ?? GatewayWebAppFactory.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "TaskManager.Identity",
            audience: "TaskManager",
            claims:
            [
                new Claim("sub", userId.ToString()),
                new Claim("email", email),
                new Claim("jti", Guid.NewGuid().ToString()),
            ],
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
