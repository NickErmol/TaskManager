using Microsoft.AspNetCore.Identity;
using TaskManager.Identity.Domain.Entities;

namespace TaskManager.Identity.Infrastructure.Services;

/// <summary>
/// Replaces ASP.NET Core Identity's default PBKDF2 password hasher with BCrypt at work factor 12
/// (spec §4.2). Registered as the IPasswordHasher&lt;AppUser&gt; implementation in DI.
/// </summary>
public class BcryptPasswordHasher : IPasswordHasher<AppUser>
{
    private const int WorkFactor = 12;

    public string HashPassword(AppUser user, string password)
        => BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor);

    public PasswordVerificationResult VerifyHashedPassword(AppUser user, string hashedPassword, string providedPassword)
    {
        try
        {
            var ok = BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
            if (!ok) return PasswordVerificationResult.Failed;

            // If the stored hash uses a lower work factor than current, ask Identity to rehash.
            var currentWorkFactor = ExtractWorkFactor(hashedPassword);
            return currentWorkFactor < WorkFactor
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Success;
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return PasswordVerificationResult.Failed;
        }
    }

    private static int ExtractWorkFactor(string hash)
    {
        // BCrypt hash format: $2a$<workFactor>$<22-char salt><31-char hash>
        if (hash.Length < 7 || hash[0] != '$') return 0;
        var dollarIdx = hash.IndexOf('$', 4);
        if (dollarIdx <= 4) return 0;
        return int.TryParse(hash.AsSpan(4, dollarIdx - 4), out var wf) ? wf : 0;
    }
}
