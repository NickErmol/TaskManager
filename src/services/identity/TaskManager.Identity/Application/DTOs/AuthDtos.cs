namespace TaskManager.Identity.Application.DTOs;

public record RegisterRequest(string Email, string DisplayName, string Password);

public record LoginRequest(string Email, string Password);

public record UpdateProfileRequest(string DisplayName, string? AvatarUrl);

public record UserDto(Guid Id, string Email, string DisplayName, string? AvatarUrl, DateTimeOffset CreatedAt);

/// <summary>
/// Response body returned to the SPA. Refresh token is NOT included here — it is set as a
/// HttpOnly; Secure; SameSite=Strict cookie by the presentation layer (spec §4.2).
/// </summary>
public record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

/// <summary>
/// Internal handler result. Carries the refresh-token plaintext so the endpoint can attach
/// it to the cookie. Never serialised to JSON.
/// </summary>
public record AuthHandlerResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshTokenPlaintext,
    DateTimeOffset RefreshTokenExpiresAt,
    UserDto User);
