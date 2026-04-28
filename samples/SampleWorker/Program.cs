using Lantern.Telemetry.AspNetCore;
using SampleWorker;

var builder = Host.CreateApplicationBuilder(args);

// ── 1. Register Lantern (same 3-line call as SampleApi) ──────────────────────
builder.Services.AddLanternTelemetry(opts =>
{
    opts.CollectorEndpoint = builder.Configuration["Lantern:CollectorEndpoint"]
        ?? "http://localhost:8080";
    opts.ApiKey    = builder.Configuration["Lantern:ApiKey"]    ?? "";
    opts.ProjectId = builder.Configuration["Lantern:ProjectId"] ?? "";
});

// ── 2. Register the background worker ────────────────────────────────────────
builder.Services.AddHostedService<OrderProcessingWorker>();

var host = builder.Build();
host.Run();
