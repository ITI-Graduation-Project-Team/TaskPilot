using System.Threading.Channels;
using TaskPilot.AI.Models.Telemetry;

namespace TaskPilot.Services.Interfaces;

public interface IAiTelemetryQueue
{
    ChannelReader<AiUsageRecord> Reader { get; }
    bool TryEnqueue(AiUsageRecord record);
    void Complete();
}
