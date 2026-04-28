using Lantern.Telemetry;
using Lantern.Telemetry.Coverage;
using Lantern.Telemetry.Emission;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lantern.Telemetry.AspNetCore;

public static class LanternServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Lantern services. Call <c>UseLanternTelemetry()</c> on the app to activate middleware.
    /// </summary>
    public static IServiceCollection AddLanternTelemetry(
        this IServiceCollection services,
        Action<LanternOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IValidateOptions<LanternOptions>, LanternOptionsValidator>();
        services.AddSingleton<LanternRuntimeContext>();

        services.AddSingleton<ICoverageBackend>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<LanternOptions>>().Value;
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CoverletCoverageBackend>>();
            return new CoverletCoverageBackend(logger);
        });

        services.AddSingleton<CoverageManager>();
        services.AddSingleton<CoverageEventQueue>();
        services.AddSingleton<LanternClient>(sp => new LanternClient(
            sp.GetRequiredService<CoverageManager>(),
            sp.GetRequiredService<CoverageEventQueue>(),
            sp.GetRequiredService<IOptions<LanternOptions>>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LanternClient>>()));

        services.AddHttpClient<EventFlusher>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<LanternOptions>>().Value;
            client.BaseAddress = new Uri(opts.CollectorEndpoint);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {opts.ApiKey}");
            client.Timeout = opts.HttpTimeout;
        })
        .AddStandardResilienceHandler();

        services.AddHostedService<LanternBackgroundService>();

        return services;
    }
}
