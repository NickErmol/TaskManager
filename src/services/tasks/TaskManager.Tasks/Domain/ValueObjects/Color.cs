using System.Text.RegularExpressions;
using FluentResults;

namespace TaskManager.Tasks.Domain.ValueObjects;

/// <summary>
/// Hex colour value object. Mapped as an EF Core owned entity of <c>Label</c> so it persists
/// as a single column (<c>labels.color</c>) rather than a separate table — spec §4.3.
/// </summary>
public partial record Color
{
    public string Value { get; init; } = default!;

    // Parameterless ctor used by EF Core materialisation.
    private Color() { }

    private Color(string value) => Value = value;

    public static Result<Color> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !HexColorRegex().IsMatch(value))
            return Result.Fail<Color>("Color must be a valid hex string e.g. #4ade80");
        return Result.Ok(new Color(value));
    }

    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();
}
