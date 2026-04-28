# Recipe: MassTransit

Extract the Lantern test ID from a MassTransit message header and establish a coverage scope around message processing.

## Setup

No extra packages required. MassTransit exposes headers via `ConsumeContext.Headers`.

## Consumer implementation

```csharp
using Lantern.Telemetry;
using MassTransit;

public sealed class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var testId = context.Headers.Get<string>("lantern.test_id");

        if (testId is not null)
        {
            var testName = context.Headers.Get<string>("lantern.test_name");
            using var scope = LanternScope.ForTest(testId, testName);
            await ProcessAsync(context.Message);
        }
        else
        {
            await ProcessAsync(context.Message);
        }
    }

    private Task ProcessAsync(OrderCreated message)
    {
        // ... your business logic ...
        return Task.CompletedTask;
    }
}
```

## Sending a message with headers (from Playwright test setup)

The Playwright plugin sets these headers automatically when it publishes via the Lantern-aware HTTP client. For manual testing:

```csharp
await publishEndpoint.Publish(new OrderCreated { OrderId = "123" }, ctx =>
{
    ctx.Headers.Set("lantern.test_id", testId);
    ctx.Headers.Set("lantern.test_name", "should process order");
});
```

## Notes

- `context.Headers.Get<string>` returns `null` when the header is absent; no guard needed.
- The scope is established per-message. If your consumer dispatches sub-tasks on other threads,
  wrap each with `LanternScope.ForTest` as well — `AsyncLocal` is inherited by child tasks
  but not by `Task.Run` continuations that outlive the scope's `using` block.
