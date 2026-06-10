namespace TaskManager.E2E.Tests.Infrastructure;

/// <summary>
/// Where the running stack lives. Defaults match local dev
/// (docker compose up -d --build + ng serve); override via env vars in CI/staging.
/// </summary>
public static class E2eConfig
{
    public static string FrontendUrl { get; } = Get("E2E_FRONTEND_URL", "http://localhost:4200");
    public static string GatewayUrl { get; } = Get("E2E_GATEWAY_URL", "http://localhost:5000");
    public static string MailhogUrl { get; } = Get("E2E_MAILHOG_URL", "http://localhost:8025");
    public static string SeqUrl { get; } = Get("E2E_SEQ_URL", "http://localhost:5341");

    public static bool Headed { get; } = Get("E2E_HEADED", "false") == "true";

    private static string Get(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;
}
