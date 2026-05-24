using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "Analytics"));

builder.Services.AddHealthChecks();

// EF Core read models, MassTransit consumers (with EF Core inbox), endpoints land in Step 5b.

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "analytics", status = "scaffold" }));

app.Run();

public partial class Program;
