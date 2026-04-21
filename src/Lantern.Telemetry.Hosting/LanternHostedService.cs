using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Lantern.Telemetry.Hosting;

public sealed class LanternHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
