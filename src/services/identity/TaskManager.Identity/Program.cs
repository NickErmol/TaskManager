using System.Text;
using FluentResults;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using TaskManager.Identity.Application.Behaviors;
using TaskManager.Identity.Application.Mappers;
using TaskManager.Identity.Infrastructure;
using TaskManager.Identity.Infrastructure.Persistence;
using TaskManager.Identity.Infrastructure.Services;
using TaskManager.Identity.Presentation.Endpoints;
using TaskManager.Identity.Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "Identity"));

// Infrastructure (DbContext, repositories, TokenService, password hasher, Identity, JWT options)
builder.Services.AddIdentityInfrastructure(builder.Configuration);

// JWT bearer authentication (Identity validates its own tokens for /api/users/* and /logout)
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? builder.Configuration["Jwt:SecretKey"] ?? string.Empty;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
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
    });
builder.Services.AddAuthorization();

// Mediator + pipeline behaviors + Mapperly + validators
builder.Services.AddMediator(opt => opt.ServiceLifetime = ServiceLifetime.Scoped);
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddSingleton<IdentityMapper>();

// Health checks
// Env var first (spec §8) and empty-as-missing — same precedence as AddIdentityInfrastructure.
var connectionForHealth = builder.Configuration["IDENTITY_DB_CONNECTION"];
if (string.IsNullOrWhiteSpace(connectionForHealth))
    connectionForHealth = builder.Configuration["ConnectionStrings:IdentityDb"] ?? string.Empty;
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddNpgSql(_ => connectionForHealth, name: "postgres", tags: new[] { "ready" });

var app = builder.Build();

// Apply EF migrations on startup (spec §8)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapAuthEndpoints(app.Environment);
app.MapUserEndpoints();

app.Run();

public partial class Program;
