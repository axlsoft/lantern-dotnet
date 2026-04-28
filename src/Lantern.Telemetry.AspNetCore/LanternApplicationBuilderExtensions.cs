using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lantern.Telemetry.AspNetCore;

public static class LanternApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Lantern middleware and control-plane endpoints. Must be called after
    /// <c>UseRouting()</c> if using endpoint routing.
    /// </summary>
    public static IApplicationBuilder UseLanternTelemetry(this IApplicationBuilder app)
    {
        var opts = app.ApplicationServices.GetRequiredService<IOptions<LanternOptions>>().Value;
        var logger = app.ApplicationServices
            .GetRequiredService<ILogger<LanternMiddleware>>();

        if (!opts.Enabled)
        {
            logger.LogInformation("Lantern telemetry is disabled (Enabled=false). Skipping middleware.");
            return app;
        }

        if (opts.ControlPlaneEnabled &&
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
        {
            logger.LogWarning(
                "Lantern control plane is enabled in Production. " +
                "Set LanternOptions.ControlPlaneEnabled=false or restrict access to {Path}.",
                opts.ControlPlanePath);
        }

        app.UseMiddleware<LanternMiddleware>();

        if (opts.ControlPlaneEnabled)
            ControlPlane.ControlPlaneHandler.Map(app, opts.ControlPlanePath);

        return app;
    }
}
