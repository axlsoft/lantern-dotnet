# Recipe: NServiceBus

Extract the Lantern test ID from an NServiceBus message header and establish a coverage scope.

## Setup

No extra packages required. NServiceBus exposes headers via `IMessageHandlerContext.MessageHeaders`.

## Handler implementation

```csharp
using Lantern.Telemetry;
using NServiceBus;

public sealed class OrderCreatedHandler : IHandleMessages<OrderCreated>
{
    public async Task Handle(OrderCreated message, IMessageHandlerContext context)
    {
        context.MessageHeaders.TryGetValue("lantern.test_id", out var testId);

        if (testId is not null)
        {
            context.MessageHeaders.TryGetValue("lantern.test_name", out var testName);
            using var scope = LanternScope.ForTest(testId, testName);
            await ProcessAsync(message);
        }
        else
        {
            await ProcessAsync(message);
        }
    }

    private Task ProcessAsync(OrderCreated message)
    {
        // ... your business logic ...
        return Task.CompletedTask;
    }
}
```

## Sending a message with headers

```csharp
var options = new SendOptions();
options.SetHeader("lantern.test_id", testId);
options.SetHeader("lantern.test_name", "should process order");
await session.Send(new OrderCreated { OrderId = "123" }, options);
```

## Notes

- `MessageHeaders.TryGetValue` returns `false` when the header is absent; `testId` will be `null`.
- NServiceBus pipeline behaviors can centralise header extraction if you prefer to avoid
  per-handler boilerplate — implement `IBehavior<IIncomingLogicalMessageContext, IIncomingLogicalMessageContext>`
  and call `LanternScope.ForTest` in the behavior before calling `next`.
