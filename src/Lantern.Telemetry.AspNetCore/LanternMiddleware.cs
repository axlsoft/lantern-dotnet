using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lantern.Telemetry.AspNetCore;

/// <summary>
/// Reads the W3C traceparent header, establishes a <see cref="LanternScope"/>, and wraps
/// the request in a coverage window via <see cref="LanternClient"/>.
/// </summary>
public sealed class LanternMiddleware
{
    private const string TraceparentHeader = "traceparent";
    private const string BaggageHeader = "baggage";

    private readonly RequestDelegate _next;
    private readonly LanternClient _client;
    private readonly LanternOptions _options;
    private readonly ILogger<LanternMiddleware> _logger;

    public LanternMiddleware(
        RequestDelegate next,
        LanternClient client,
        IOptions<LanternOptions> options,
        ILogger<LanternMiddleware> logger)
    {
        _next = next;
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        var (testId, testName) = ExtractTestContext(context.Request);

        if (testId is null)
        {
            await _next(context);
            return;
        }

        _logger.LogDebug("Lantern scope: test_id={TestId}", testId);

        using var lanternScope = LanternScope.ForTest(testId, testName);
        await using var coverageScope = await _client.BeginTestScopeAsync(
            testId, testName, cancellationToken: context.RequestAborted);

        await _next(context);
    }

    private static (string? TestId, string? TestName) ExtractTestContext(HttpRequest request)
    {
        // Require a valid W3C traceparent — Lantern only activates when the caller
        // participates in distributed tracing, preventing accidental activation
        // from stray baggage headers in production traffic.
        if (!request.Headers.TryGetValue(TraceparentHeader, out var tpValues))
            return (null, null);

        var traceparent = tpValues.FirstOrDefault();
        if (traceparent is null || !ActivityContext.TryParse(traceparent, null, out _))
            return (null, null);

        // Test ID and name are carried in W3C baggage per ADR-013.
        // Playwright injects: baggage: lantern.test_id=<id>,lantern.test_name=<name>
        if (!request.Headers.TryGetValue(BaggageHeader, out var baggageValues))
            return (null, null);

        string? testId = null;
        string? testName = null;

        foreach (var entry in baggageValues.ToString().Split(','))
        {
            var kv = entry.Trim().Split('=', 2);
            if (kv.Length != 2) continue;
            switch (kv[0].Trim())
            {
                case "lantern.test_id":
                    testId = Uri.UnescapeDataString(kv[1].Trim());
                    break;
                case "lantern.test_name":
                    testName = Uri.UnescapeDataString(kv[1].Trim());
                    break;
            }
        }

        return (testId, testName);
    }
}
