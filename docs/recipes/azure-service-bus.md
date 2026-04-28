# Recipe: Azure Service Bus

Extract the Lantern test ID from an Azure Service Bus message and establish a coverage scope.

## Setup

Uses `Azure.Messaging.ServiceBus`. No extra packages required.

## Processor callback

```csharp
using Lantern.Telemetry;
using Azure.Messaging.ServiceBus;

ServiceBusClient client = new(connectionString);
ServiceBusProcessor processor = client.CreateProcessor(queueName);

processor.ProcessMessageAsync += async args =>
{
    args.Message.ApplicationProperties.TryGetValue("lantern.test_id", out var testIdObj);
    var testId = testIdObj as string;

    if (testId is not null)
    {
        args.Message.ApplicationProperties.TryGetValue("lantern.test_name", out var testNameObj);
        var testName = testNameObj as string;

        using var scope = LanternScope.ForTest(testId, testName);
        await ProcessAsync(args.Message);
    }
    else
    {
        await ProcessAsync(args.Message);
    }

    await args.CompleteMessageAsync(args.Message);
};

static Task ProcessAsync(ServiceBusReceivedMessage message)
{
    // ... your business logic ...
    return Task.CompletedTask;
}
```

## Sending a message with properties

```csharp
var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(payload))
{
    ApplicationProperties =
    {
        ["lantern.test_id"] = testId,
        ["lantern.test_name"] = "should process order"
    }
};

await sender.SendMessageAsync(message);
```

## Notes

- `ApplicationProperties` values are `object`; cast to `string` after `TryGetValue`.
- If you use `ServiceBusSessionProcessor`, the same pattern applies inside
  `ProcessSessionMessageAsync`.
