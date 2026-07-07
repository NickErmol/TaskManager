using System.Text;
using FluentAssertions;
using TaskManager.Tasks.Application.Attachments;
using Xunit;

namespace TaskManager.Tasks.Tests.Unit;

public class AttachmentPolicyTests
{
    [Theory]
    [InlineData("a.png", "image/png", true)]
    [InlineData("a.PDF", "application/pdf", true)]
    [InlineData("a.exe", "application/octet-stream", false)]   // extension not allowed
    [InlineData("a.png", "text/html", false)]                  // content-type mismatch
    public void IsAllowed_checks_extension_and_content_type(string name, string ct, bool expected)
        => AttachmentPolicy.IsAllowed(name, ct).Should().Be(expected);

    [Fact]
    public void ExceedsMaxBytes_true_above_10MB()
    {
        AttachmentPolicy.ExceedsMaxBytes(AttachmentPolicy.MaxBytes + 1).Should().BeTrue();
        AttachmentPolicy.ExceedsMaxBytes(AttachmentPolicy.MaxBytes).Should().BeFalse();
    }

    [Fact]
    public void MagicBytesMatch_true_for_real_png_false_for_spoof()
    {
        byte[] png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0 };
        byte[] notPng = Encoding.ASCII.GetBytes("<html>hello</html>");
        AttachmentPolicy.MagicBytesMatch("image/png", png).Should().BeTrue();
        AttachmentPolicy.MagicBytesMatch("image/png", notPng).Should().BeFalse();
    }

    [Fact]
    public void MagicBytesMatch_true_for_types_without_a_signature()
    {
        // text/plain has no signature — accept by extension/content-type only.
        AttachmentPolicy.MagicBytesMatch("text/plain", Encoding.ASCII.GetBytes("hi")).Should().BeTrue();
    }
}
