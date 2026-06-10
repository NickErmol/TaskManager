using Microsoft.EntityFrameworkCore;
using Serilog;
using TaskManager.Analytics.Infrastructure;
using TaskManager.Analytics.Infrastructure.Persistence;
using TaskManager.Analytics.Presentation.Endpoints;
using TaskManager.Analytics.Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "Analytics"));

// DbContext + repository + MassTransit consumers with EF Core inbox
builder.Services.AddAnalyticsInfrastructure(builder.Configuration);

// Health checks per spec §8
var connectionForHealth = builder.Configuration["ANALYTICS_DB_CONNECTION"];
if (string.IsNullOrWhiteSpace(connectionForHealth))
    connectionForHealth = builder.Configuration["ConnectionStrings:AnalyticsDb"] ?? string.Empty;
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddNpgSql(_ => connectionForHealth, name: "postgres", tags: ["ready"]);

var app = builder.Build();

// Apply EF migrations on startup (spec §8)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

app.MapHealthChecks("/health");
app.MapAnalyticsEndpoints();

app.Run();

public partial class Program;
