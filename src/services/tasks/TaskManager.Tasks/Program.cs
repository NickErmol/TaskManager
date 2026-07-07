using System.Text;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using TaskManager.Tasks.Application.Behaviors;
using TaskManager.Tasks.Application.Interfaces;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Application.Services;
using TaskManager.Tasks.Infrastructure;
using TaskManager.Tasks.Infrastructure.Persistence;
using TaskManager.Tasks.Infrastructure.Realtime;
using TaskManager.Tasks.Presentation.Background;
using TaskManager.Tasks.Presentation.Endpoints;
using TaskManager.Tasks.Presentation.Hubs;
using TaskManager.Tasks.Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "Tasks"));

// Infrastructure: DbContext + repositories + MassTransit with EF outbox
builder.Services.AddTasksInfrastructure(builder.Configuration);

// Mediator + pipeline behaviors + validators + mapper
builder.Services.AddMediator(opt => opt.ServiceLifetime = ServiceLifetime.Scoped);
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddSingleton<TasksMapper>();

// Real-time board sync (spec §F3): SignalR hub + best-effort broadcaster + in-memory presence.
builder.Services.AddSignalR();
builder.Services.AddSingleton<IPresenceTracker, PresenceTracker>();
builder.Services.AddSingleton<IBoardBroadcaster, SignalRBoardBroadcaster>();

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

// Deadline scan (Application logic, Presentation hosting)
builder.Services.AddScoped<DeadlineScanner>();
builder.Services.AddHostedService<DeadlineWorker>();

// Health checks per spec §8
// Env var first (spec §8) and empty-as-missing — same precedence as AddTasksInfrastructure.
var connectionForHealth = builder.Configuration["TASKS_DB_CONNECTION"];
if (string.IsNullOrWhiteSpace(connectionForHealth))
    connectionForHealth = builder.Configuration["ConnectionStrings:TasksDb"] ?? string.Empty;
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddNpgSql(_ => connectionForHealth, name: "postgres", tags: new[] { "ready" });

// Multipart upload cap (spec §13.5) — reject oversized bodies before buffering the whole form.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = TaskManager.Tasks.Application.Attachments.AttachmentPolicy.MaxBytes;
});

var app = builder.Build();

// Apply EF migrations on startup (spec §8)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TasksDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapBoardEndpoints();
app.MapTaskEndpoints();
app.MapAttachmentEndpoints();
app.MapHub<BoardHub>("/hubs/board");

app.Run();

public partial class Program;
