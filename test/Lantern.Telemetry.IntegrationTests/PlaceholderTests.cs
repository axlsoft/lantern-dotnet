// Integration tests wiring up the ASP.NET Core adapter end-to-end.
// These tests require a running collector; configure LANTERN_COLLECTOR_ENDPOINT
// and LANTERN_API_KEY in the environment or appsettings.Test.json.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lantern.Telemetry.AspNetCore;

namespace Lantern.Telemetry.IntegrationTests;

public class AspNetCoreAdapterTests : IClassFixture<LanternWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AspNetCoreAdapterTests(LanternWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RequestWithoutTraceparent_Returns200_NoScope()
    {
        var response = await _client.GetAsync("/ping");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("no-scope", body);
    }

    [Fact]
    public async Task RequestWithLanternTraceparent_EstablishesScope()
    {
        var testId = "abcdef1234567890abcdef1234567890"; // 32-char hex test ID
        // Lantern convention: valid W3C traceparent + baggage carrying lantern.test_id
        var traceparent = $"00-{testId}-0000000000000001-01";

        using var request = new HttpRequestMessage(HttpMethod.Get, "/ping");
        request.Headers.Add("traceparent", traceparent);
        request.Headers.Add("baggage", $"lantern.test_id={testId},lantern.test_name=my-test");

        var response = await _client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(testId[..8], body); // response echoes part of the test_id
    }
}

public sealed class LanternWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddLanternTelemetry(opts =>
            {
                opts.CollectorEndpoint = "http://localhost:8080";
                opts.ApiKey = "lntn_test_integration";
                opts.ProjectId = "integration-test";
                opts.Enabled = false; // disable real emission in unit-mode integration tests
            });
        });
    }
}

// Minimal sample Program used by the factory — see samples/SampleApi for a full example.
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();
        app.MapGet("/ping", (HttpContext ctx) =>
        {
            var scope = LanternScope.Current;
            return scope is not null
                ? Results.Ok(new { scope = scope.TestId })
                : Results.Ok(new { scope = "no-scope" });
        });
        app.Run();
    }
}
