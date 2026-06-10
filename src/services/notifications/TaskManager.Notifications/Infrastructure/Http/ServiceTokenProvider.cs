using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TaskManager.Notifications.Infrastructure.Http;

/// <summary>
/// Mints a short-lived service JWT with the shared JWT_SECRET so this service can
/// call Identity's authorized endpoints (GET /api/users/{id}) for email resolution.
/// </summary>
public class ServiceTokenProvider(IConfiguration config)
{
    private readonly object _gate = new();
    private string? _token;
    private DateTimeOffset _expires;

    public string GetToken()
    {
        lock (_gate)
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expires - TimeSpan.FromMinutes(1))
                return _token;

            var secret = config["JWT_SECRET"] ?? config["Jwt:SecretKey"];
            if (string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException("JWT_SECRET is not configured");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            _expires = DateTimeOffset.UtcNow.AddMinutes(15);
            var token = new JwtSecurityToken(
                issuer: config["Jwt:Issuer"] ?? "TaskManager.Identity",
                audience: config["Jwt:Audience"] ?? "TaskManager",
                claims:
                [
                    new Claim("sub", Guid.Empty.ToString()),
                    new Claim("email", "notifications-svc@task-manager.local"),
                    new Claim("jti", Guid.NewGuid().ToString()),
                ],
                notBefore: DateTime.UtcNow,
                expires: _expires.UtcDateTime,
                signingCredentials: creds);

            _token = new JwtSecurityTokenHandler().WriteToken(token);
            return _token;
        }
    }
}
