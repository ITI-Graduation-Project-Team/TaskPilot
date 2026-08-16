using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services.Implementations
{
    public class TokenQuotaContext : ITokenQuotaContext
    {
        public bool LimitReached { get; set; }
        public long CurrentUsage { get; set; }
        public int Limit { get; set; }
    }
}
