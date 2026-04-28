# Recipe: RabbitMQ (RabbitMQ.Client)

Extract the Lantern test ID from a RabbitMQ message and establish a coverage scope.

## Setup

Uses `RabbitMQ.Client`. No extra packages required.

## Consumer implementation

```csharp
using Lantern.Telemetry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

var consumer = new AsyncEventingBasicConsumer(channel);

consumer.ReceivedAsync += async (_, ea) =>
{
    string? testId = null;
    string? testName = null;

    if (ea.BasicProperties.Headers is { } headers)
    {
        if (headers.TryGetValue("lantern.test_id", out var rawId))
            testId = Encoding.UTF8.GetString((byte[])rawId);

        if (headers.TryGetValue("lantern.test_name", out var rawName))
            testName = Encoding.UTF8.GetString((byte[])rawName);
    }

    if (testId is not null)
    {
        using var scope = LanternScope.ForTest(testId, testName);
        await ProcessAsync(ea.Body.ToArray());
    }
    else
    {
        await ProcessAsync(ea.Body.ToArray());
    }
};

channel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);

static Task ProcessAsync(ReadOnlyMemory<byte> body)
{
    // ... your business logic ...
    return Task.CompletedTask;
}
```

## Publishing a message with headers

```csharp
var properties = new BasicProperties
{
    Headers = new Dictionary<string, object?>
    {
        ["lantern.test_id"] = Encoding.UTF8.GetBytes(testId),
        ["lantern.test_name"] = Encoding.UTF8.GetBytes("should process order")
    }
};

channel.BasicPublish(
    exchange: "",
    routingKey: queueName,
    basicProperties: properties,
    body: Encoding.UTF8.GetBytes(payload));
```

## Notes

- RabbitMQ header values are `byte[]` — always decode with `Encoding.UTF8.GetString`.
- For `IBasicConsumer` (non-async), the same pattern applies inside `HandleBasicDeliver`.
- If you use a higher-level library built on `RabbitMQ.Client` (e.g. EasyNetQ),
  check whether it surfaces headers directly; if so, read from the library's header type
  rather than `IBasicProperties.Headers`.
