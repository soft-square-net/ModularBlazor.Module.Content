using System.Text.Json;
using soft.square.net.WebApi.Contracts.Live;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.Live;

/// <summary>Records what the module publishes; <see cref="FailOn"/> makes a topic's publish throw.</summary>
public sealed class FakeLiveNotifier : ILiveNotifier
{
    public sealed record Sent(string Topic, string Type, JsonElement? Payload, long? Sequence);

    public List<Sent> Messages { get; } = [];
    public string? FailOn { get; set; }

    public Task PublishAsync(string topic, string type, JsonElement? payload = null, long? sequence = null, CancellationToken ct = default)
    {
        if (topic == FailOn) throw new InvalidOperationException("The bus is down.");
        Messages.Add(new Sent(topic, type, payload, sequence));
        return Task.CompletedTask;
    }

    public Task PublishToUnitAsync(string unitPath, string type, JsonElement? payload = null, long? sequence = null, CancellationToken ct = default) =>
        throw new NotSupportedException("Content publishes no unit messages.");
}
