using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "Tasks"));

builder.Services.AddHealthChecks();

// EF Core, Mediator, MassTransit (with EF Core outbox), endpoints land in Step 3b.

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "tasks", status = "scaffold" }));

app.Run();

public partial class Program;
