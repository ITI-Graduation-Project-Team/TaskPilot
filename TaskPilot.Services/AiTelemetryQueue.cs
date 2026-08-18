using System.Threading.Channels;
using TaskPilot.AI.Models.Telemetry;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services;

public sealed class AiTelemetryQueue : IAiTelemetryQueue
{
    private readonly Channel<AiUsageRecord> _channel = Channel.CreateBounded<AiUsageRecord>(
        new BoundedChannelOptions(10_000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    public ChannelReader<AiUsageRecord> Reader => _channel.Reader;

    public bool TryEnqueue(AiUsageRecord record) => _channel.Writer.TryWrite(record);

    public void Complete() => _channel.Writer.TryComplete();
}
