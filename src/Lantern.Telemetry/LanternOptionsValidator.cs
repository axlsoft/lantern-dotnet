using Microsoft.Extensions.Options;

namespace Lantern.Telemetry;

internal sealed class LanternOptionsValidator : IValidateOptions<LanternOptions>
{
    public ValidateOptionsResult Validate(string? name, LanternOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.CollectorEndpoint))
            errors.Add($"{nameof(LanternOptions.CollectorEndpoint)} is required.");

        if (!Uri.TryCreate(options.CollectorEndpoint, UriKind.Absolute, out _))
            errors.Add($"{nameof(LanternOptions.CollectorEndpoint)} must be a valid absolute URI.");

        if (string.IsNullOrWhiteSpace(options.ApiKey))
            errors.Add($"{nameof(LanternOptions.ApiKey)} is required.");

        if (string.IsNullOrWhiteSpace(options.ProjectId))
            errors.Add($"{nameof(LanternOptions.ProjectId)} is required.");

        if (options.BatchSize < 1 || options.BatchSize > 10_000)
            errors.Add($"{nameof(LanternOptions.BatchSize)} must be between 1 and 10,000.");

        if (options.BufferCapacity < 100)
            errors.Add($"{nameof(LanternOptions.BufferCapacity)} must be at least 100.");

        if (options.FlushInterval <= TimeSpan.Zero)
            errors.Add($"{nameof(LanternOptions.FlushInterval)} must be positive.");

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
