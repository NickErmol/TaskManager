using TaskManager.Identity.Presentation.ExternalAuth;

namespace TaskManager.Identity.Tests.Unit;

public class ExternalReturnUrlTests
{
    [Theory]
    [InlineData("/boards", "/boards")]
    [InlineData("/boards/123?filter=me", "/boards/123?filter=me")]
    [InlineData(null, "/boards")]
    [InlineData("", "/boards")]
    [InlineData("   ", "/boards")]
    [InlineData("https://evil.example.com", "/boards")]
    [InlineData("http://evil.example.com/boards", "/boards")]
    [InlineData("//evil.example.com", "/boards")]
    [InlineData(@"/\evil.example.com", "/boards")]
    [InlineData("boards", "/boards")]
    public void Sanitize_allows_only_relative_paths(string? input, string expected)
        => ExternalReturnUrl.Sanitize(input).Should().Be(expected);
}
