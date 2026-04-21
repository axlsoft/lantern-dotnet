using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Lantern.Telemetry.AspNetCore;

public sealed class LanternMiddleware
{
    private readonly RequestDelegate _next;

    public LanternMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context) => _next(context);
}
