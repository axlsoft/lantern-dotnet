using Lantern.Telemetry;
using Lantern.Telemetry.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Register Lantern (5 lines of code) ────────────────────────────────────
builder.Services.AddLanternTelemetry(opts =>
{
    opts.CollectorEndpoint = builder.Configuration["Lantern:CollectorEndpoint"]
        ?? "http://localhost:8080";
    opts.ApiKey  = builder.Configuration["Lantern:ApiKey"]  ?? "";
    opts.ProjectId = builder.Configuration["Lantern:ProjectId"] ?? "";
});

var app = builder.Build();

// ── 2. Activate middleware ────────────────────────────────────────────────────
app.UseLanternTelemetry();

// ── 3. Sample endpoints ───────────────────────────────────────────────────────
app.MapGet("/", () => "Lantern SampleApi is running.");

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// Echoes the current Lantern scope so the Playwright plugin can verify attribution.
app.MapGet("/lantern-scope", () =>
{
    var ctx = LanternScope.Current;
    return ctx is not null
        ? Results.Ok(new { test_id = ctx.TestId, test_name = ctx.TestName })
        : Results.Ok(new { test_id = (string?)null, test_name = (string?)null });
});

// A business endpoint that exercises some code under test.
app.MapGet("/orders/{id}", (string id) =>
    Results.Ok(new { id, total = 99.99m, status = "shipped" }));

app.MapPost("/orders", (CreateOrderRequest req) =>
    Results.Created($"/orders/{Guid.NewGuid()}", new { req.ProductId, req.Quantity }));

app.Run();

record CreateOrderRequest(string ProductId, int Quantity);
