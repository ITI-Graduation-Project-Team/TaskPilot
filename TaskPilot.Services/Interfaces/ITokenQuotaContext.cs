namespace TaskPilot.Services.Interfaces
{
    public interface ITokenQuotaContext
    {
        bool LimitReached { get; set; }
        long CurrentUsage { get; set; }
        int Limit { get; set; }
    }
}
