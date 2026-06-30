using Microsoft.AspNetCore.Authorization;
using TaskPilot.Models.Common;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class SkillsController : ApiControllerBase
    {
        private readonly ISkillService _skillService;
        private readonly IUnitOfWork _unitOfWork;

        public SkillsController(ISkillService skillService, IUnitOfWork unitOfWork)
        {
            _skillService = skillService;
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _skillService.GetAllAsync();
            return HandleResult(result, SuccessCodes.Skill.Retrieved);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] string name)
        {
            var result = await _skillService.CreateAsync(name);

            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result, SuccessCodes.Skill.Created);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _skillService.DeleteAsync(id);

            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result, SuccessCodes.Skill.Deleted);
        }
        [HttpPost("bulk")]
        public async Task<IActionResult> CreateBulk([FromBody] List<string> names)
        {
            var result = await _skillService.CreateBulkAsync(names);

            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result, SuccessCodes.Skill.Created);
        }
    }
}

