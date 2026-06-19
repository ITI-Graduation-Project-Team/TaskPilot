using Microsoft.AspNetCore.Mvc;
using TaskPilot.Models.Common;
using TaskPilot.Data.Repositories;
using TaskPilot.Services.Interfaces;
using TaskPilot.DTOs.Projects;

namespace TaskPilot.Presentation.Controllers
{
    public class ProjectsController : ApiControllerBase
    {
        private readonly IProjectService _projectService;
        private readonly IUnitOfWork _unitOfWork;

        public ProjectsController(IProjectService projectService, IUnitOfWork unitOfWork)
        {
            _projectService = projectService;
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public async Task<ActionResult> GetAll()
        {
            var result = await _projectService.GetAllAsync();
            return HandleResult(result, SuccessCodes.Project.Retrieved);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult> GetById(Guid id)
        {
            var result = await _projectService.GetByIdAsync(id);
            return HandleResult(result, SuccessCodes.Project.Retrieved);
        }

        [HttpGet("company/{companyId:guid}")]
        public async Task<ActionResult> GetByCompanyId(Guid companyId)
        {
            var result = await _projectService.GetByCompanyIdAsync(companyId);
            return HandleResult(result, SuccessCodes.Project.Retrieved);
        }

        [HttpPost]
        public async Task<ActionResult> Create([FromBody] CreateProjectDto project)
        {
            var result = await _projectService.CreateAsync(project);

            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleCreated(result, SuccessCodes.Project.Created);
        }

        [HttpPut]
        public async Task<ActionResult> Update([FromBody] UpdateProjectDto project)
        {
            var result = await _projectService.UpdateAsync(project);

            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result, SuccessCodes.Project.Updated);
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> Delete(Guid id)
        {
            var result = await _projectService.DeleteAsync(id);

            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result, SuccessCodes.Project.Deleted);
        }
    }
}
