using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "Notifications"));

builder.Services.AddHealthChecks();
builder.Services.AddSignalR();

// MassTransit consumers, Redis, MailKit, SignalR hub land in Step 4b.

var app = builder.Build();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "notifications", status = "scaffold" }));

app.Run();

public partial class Program;
