namespace TaskManager.Tasks.Application.Attachments;

/// <summary>
/// Upload policy (spec §13.5): 10 MB cap, extension+content-type whitelist, and a magic-byte
/// sniff for declared image/PDF types to defeat type spoofing.
/// </summary>
public static class AttachmentPolicy
{
    public const long MaxBytes = 10 * 1024 * 1024;

    // extension (lower, with dot) -> acceptable content-types
    private static readonly Dictionary<string, HashSet<string>> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"]  = new(StringComparer.OrdinalIgnoreCase) { "image/png" },
        [".jpg"]  = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg" },
        [".jpeg"] = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg" },
        [".gif"]  = new(StringComparer.OrdinalIgnoreCase) { "image/gif" },
        [".webp"] = new(StringComparer.OrdinalIgnoreCase) { "image/webp" },
        [".pdf"]  = new(StringComparer.OrdinalIgnoreCase) { "application/pdf" },
        [".txt"]  = new(StringComparer.OrdinalIgnoreCase) { "text/plain" },
        [".csv"]  = new(StringComparer.OrdinalIgnoreCase) { "text/csv", "application/vnd.ms-excel" },
        [".zip"]  = new(StringComparer.OrdinalIgnoreCase) { "application/zip", "application/x-zip-compressed" },
        [".docx"] = new(StringComparer.OrdinalIgnoreCase) { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        [".xlsx"] = new(StringComparer.OrdinalIgnoreCase) { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        [".pptx"] = new(StringComparer.OrdinalIgnoreCase) { "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
    };

    // content-type -> required leading bytes (only for types with a stable signature)
    private static readonly Dictionary<string, byte[]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"]       = new byte[] { 0x89, 0x50, 0x4E, 0x47 },
        ["image/gif"]       = new byte[] { 0x47, 0x49, 0x46, 0x38 },  // "GIF8"
        ["image/jpeg"]      = new byte[] { 0xFF, 0xD8, 0xFF },
        ["application/pdf"] = new byte[] { 0x25, 0x50, 0x44, 0x46 },  // "%PDF"
        ["application/zip"] = new byte[] { 0x50, 0x4B, 0x03, 0x04 },  // also docx/xlsx/pptx (zip containers)
    };

    public static bool ExceedsMaxBytes(long sizeBytes) => sizeBytes > MaxBytes;

    public static bool IsAllowed(string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext)
            && Allowed.TryGetValue(ext, out var types)
            && types.Contains(contentType);
    }

    /// <summary>True if the declared content-type has no signature or the header matches it.</summary>
    public static bool MagicBytesMatch(string contentType, ReadOnlySpan<byte> header)
    {
        if (!Signatures.TryGetValue(contentType, out var sig)) return true; // no signature to enforce
        if (header.Length < sig.Length) return false;
        return header[..sig.Length].SequenceEqual(sig);
    }
}
