using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using TaskManager.Notifications.Application.Interfaces;
using TaskManager.Notifications.Infrastructure;
using TaskManager.Notifications.Presentation.Endpoints;
using TaskManager.Notifications.Presentation.Hubs;
using TaskManager.Notifications.Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "Notifications"));

// Redis stores, MailKit, Identity directory, MassTransit consumers
builder.Services.AddNotificationsInfrastructure(builder.Configuration);

// SignalR + broadcaster (Presentation adapter for the Application port)
builder.Services.AddSignalR();
builder.Services.AddSingleton<INotificationBroadcaster, SignalRNotificationBroadcaster>();

// JWT auth for the hub. REST endpoints trust the gateway's X-User-Id header (spec §8);
// the WS handshake can't carry an Authorization header, so the token rides the query
// string and is lifted here for /hubs/* paths (spec §4.4).
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? builder.Configuration["Jwt:SecretKey"] ?? string.Empty;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "TaskManager.Identity",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "TaskManager",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

// Health checks per spec §8
var redisForHealth = builder.Configuration["REDIS_URL"];
if (string.IsNullOrWhiteSpace(redisForHealth))
    redisForHealth = builder.Configuration["ConnectionStrings:Redis"] ?? string.Empty;
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddRedis(redisForHealth, name: "redis", tags: ["ready"]);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapNotificationEndpoints();

app.Run();

public partial class Program;
