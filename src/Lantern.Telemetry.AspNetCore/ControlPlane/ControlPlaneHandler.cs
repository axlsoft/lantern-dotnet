using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Lantern.Telemetry.AspNetCore.ControlPlane;

/// <summary>
/// Maps the /_lantern control-plane endpoints used by the Playwright plugin to
/// start/stop test scopes independently of traceparent headers.
/// </summary>
internal static class ControlPlaneHandler
{
    internal static void Map(IApplicationBuilder app, string basePath)
    {
        app.Map(basePath, branch =>
        {
            branch.Run(async context =>
            {
                var client = context.RequestServices.GetRequiredService<LanternClient>();
                var path = context.Request.Path.Value ?? "";

                if (context.Request.Method == HttpMethods.Get && path == "/health")
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"status\":\"ok\"}");
                    return;
                }

                if (context.Request.Method == HttpMethods.Post && path == "/test/start")
                {
                    var testId = context.Request.Query["test_id"].FirstOrDefault()
                        ?? context.Request.Headers["X-Lantern-Test-Id"].FirstOrDefault();
                    var testName = context.Request.Query["test_name"].FirstOrDefault();

                    if (string.IsNullOrWhiteSpace(testId))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("{\"error\":\"test_id is required\"}");
                        return;
                    }

                    LanternScope.Set(new LanternContext(testId, testName, null, null));
                    await client.BeginTestScopeAsync(testId, testName,
                        cancellationToken: context.RequestAborted);

                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync($"{{\"test_id\":\"{testId}\"}}");
                    return;
                }

                if (context.Request.Method == HttpMethods.Post && path == "/test/stop")
                {
                    var testId = context.Request.Query["test_id"].FirstOrDefault()
                        ?? context.Request.Headers["X-Lantern-Test-Id"].FirstOrDefault()
                        ?? LanternScope.Current?.TestId;

                    if (string.IsNullOrWhiteSpace(testId))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("{\"error\":\"test_id is required\"}");
                        return;
                    }

                    await client.EndTestScopeAsync(testId, context.RequestAborted);
                    LanternScope.Set(null);

                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync($"{{\"test_id\":\"{testId}\"}}");
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status404NotFound;
            });
        });
    }
}
