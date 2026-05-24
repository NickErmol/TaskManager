using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "Gateway"));

builder.Services.AddHealthChecks();

// YARP routes, JWT auth, CORS, rate-limiting and security headers land in Step 6.

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "gateway", status = "scaffold" }));

app.Run();

public partial class Program;
