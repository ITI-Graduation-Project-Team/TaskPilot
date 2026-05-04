using TaskPilot.Services.Interfaces;

namespace TaskPilot.Data
{
    public class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
    }
}
