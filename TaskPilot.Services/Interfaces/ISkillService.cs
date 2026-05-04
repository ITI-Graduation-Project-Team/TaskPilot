using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;

namespace TaskPilot.Services.Interfaces
{
    public interface ISkillService
    {
        Task<Result<List<Skill>>> GetAllAsync();
        Task<Result<Skill>> CreateAsync(string name);
        Task<Result<List<Skill>>> CreateBulkAsync(List<string> names);
        Task<Result> DeleteAsync(int id);

    }
}
