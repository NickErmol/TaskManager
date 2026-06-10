using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TaskManager.Tasks.Application.Behaviors;
using TaskManager.Tasks.Application.Mappers;
using TaskManager.Tasks.Application.Services;
using TaskManager.Tasks.Infrastructure;
using TaskManager.Tasks.Infrastructure.Persistence;
using TaskManager.Tasks.Presentation.Background;
using TaskManager.Tasks.Presentation.Endpoints;
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

// Deadline scan (Application logic, Presentation hosting)
builder.Services.AddScoped<DeadlineScanner>();
builder.Services.AddHostedService<DeadlineWorker>();

// Health checks per spec §8
var connectionForHealth = builder.Configuration["ConnectionStrings:TasksDb"]
                          ?? builder.Configuration["TASKS_DB_CONNECTION"]
                          ?? string.Empty;
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddNpgSql(_ => connectionForHealth, name: "postgres", tags: new[] { "ready" });

var app = builder.Build();

// Apply EF migrations on startup (spec §8)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TasksDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

app.MapHealthChecks("/health");
app.MapBoardEndpoints();
app.MapTaskEndpoints();

app.Run();

public partial class Program;
