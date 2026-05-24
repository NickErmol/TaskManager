using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "Identity"));

builder.Services.AddHealthChecks();

// EF Core, Identity, JWT, Mediator, FluentValidation pipeline land in Step 2b.

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "identity", status = "scaffold" }));

app.Run();

public partial class Program;
