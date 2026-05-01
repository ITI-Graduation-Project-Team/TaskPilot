using Microsoft.AspNetCore.Identity;
using TaskPilot.Models.Entities;
namespace TaskPilot.Models.Entities
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public User? User { get; set; }   
    }
}
