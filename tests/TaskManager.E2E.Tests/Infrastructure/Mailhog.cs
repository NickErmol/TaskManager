using System.Net.Http.Json;
using System.Text.Json;

namespace TaskManager.E2E.Tests.Infrastructure;

/// <summary>Thin client for Mailhog's search API (local SMTP sink, spec §4.4).</summary>
public static class Mailhog
{
    public static async Task<bool> WaitForEmailAsync(
        HttpClient http,
        string toAddress,
        string? subjectContains = null,
        TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        var url = $"{E2eConfig.MailhogUrl}/api/v2/search?kind=to&query={Uri.EscapeDataString(toAddress)}";

        while (DateTimeOffset.UtcNow < deadline)
        {
            var result = await http.GetFromJsonAsync<JsonElement>(url);
            foreach (var item in result.GetProperty("items").EnumerateArray())
            {
                if (subjectContains is null) return true;
                var subject = item.GetProperty("Content").GetProperty("Headers")
                    .GetProperty("Subject")[0].GetString() ?? string.Empty;
                if (subject.Contains(subjectContains, StringComparison.OrdinalIgnoreCase)) return true;
            }
            await Task.Delay(2000);
        }
        return false;
    }
}
