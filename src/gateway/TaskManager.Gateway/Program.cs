using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using TaskManager.Gateway.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "Gateway"));

// JWT validation — env var first (spec §8), same precedence as the Identity service.
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? builder.Configuration["Jwt:SecretKey"] ?? string.Empty;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.MapInboundClaims = false; // keep raw `sub` / `email` claim names
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = string.IsNullOrWhiteSpace(jwtSecret)
                ? null
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        opt.Events = new JwtBearerEvents
        {
            // Browsers cannot set Authorization headers on the WebSocket handshake;
            // SignalR sends the token as ?access_token= on /hubs/ paths (spec §4.4).
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/hubs")
                    && ctx.Request.Query.TryGetValue("access_token", out var token))
                {
                    ctx.Token = token;
                }
                return Task.CompletedTask;
            },
        };
    });

// CORS for the Angular dev server — AllowCredentials is required for the refresh cookie.
const string corsPolicy = "Frontend";
builder.Services.AddCors(opt => opt.AddPolicy(corsPolicy, policy => policy
    .WithOrigins("http://localhost:4200")
    .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
    .WithHeaders("Authorization", "Content-Type", "X-Correlation-Id", "If-Match")
    .AllowCredentials()));

// Rate limiting: 100 req/min per IP globally; the credential endpoints
// (login/register/refresh) carry an additional 10 req/min policy via route config.
static RateLimitPartition<string> PerIpFixedWindow(HttpContext ctx, int permitLimit) =>
    RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });

builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opt.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => PerIpFixedWindow(ctx, 100));
    opt.AddPolicy("auth", ctx => PerIpFixedWindow(ctx, 10));
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors(corsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<JwtHeaderForwardingMiddleware>();

app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();

public partial class Program;
