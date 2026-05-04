namespace TaskPilot.Services.Interfaces
{
    public interface ICurrentUserService
    {
        Guid? UserId { get; }
    }
}
