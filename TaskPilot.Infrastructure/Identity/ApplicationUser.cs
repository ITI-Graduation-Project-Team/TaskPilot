using Microsoft.AspNetCore.Identity;
using TaskPilot.Domain.Entities;
namespace TaskPilot.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public User? User { get; set; }
    }
}
