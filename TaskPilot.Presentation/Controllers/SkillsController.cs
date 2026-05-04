using Microsoft.AspNetCore.Authorization;
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
            return HandleResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] string name)
        {
            var result = await _skillService.CreateAsync(name);

            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _skillService.DeleteAsync(id);

            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result);
        }
        [HttpPost("bulk")]
        public async Task<IActionResult> CreateBulk([FromBody] List<string> names)
        {
            var result = await _skillService.CreateBulkAsync(names);

            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result, "Skills added successfully.");
        }
    }
}

