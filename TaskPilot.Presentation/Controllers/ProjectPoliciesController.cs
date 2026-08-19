using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.AI.ProjectPolicies;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Authorize(Roles = "ProjectManager,Employee")]
    [Route("api/project-policies")]
    public class ProjectPoliciesController : ApiControllerBase
    {
        private readonly IProjectPolicyService _projectPolicyService;
        private readonly IUnitOfWork _unitOfWork;

        public ProjectPoliciesController(IProjectPolicyService projectPolicyService, IUnitOfWork unitOfWork)
        {
            _projectPolicyService = projectPolicyService;
            _unitOfWork = unitOfWork;
        }

        [HttpPost("upload")]
        [Authorize(Roles = "ProjectManager")]
        public async Task<ActionResult> Upload(
            [FromForm] UploadProjectPolicyRequest request,
            CancellationToken cancellationToken)
        {
            var ingestRequest = new IngestProjectPolicyRequest
            {
                ProjectId = request.ProjectId,
                RequirementSessionId = request.RequirementSessionId,
                File = request.File,
                TitleEn = request.File?.FileName
            };

            var result = await _projectPolicyService.IngestAsync(
                ingestRequest,
                async ct => await _unitOfWork.SaveChangesAsync(ct),
                cancellationToken);

            return HandleResult(result);
        }

        [HttpPost("ask")]
        public async Task<ActionResult> Ask(
            [FromBody] ProjectPolicyQuestionRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _projectPolicyService.AskAsync(
                request,
                canUploadPolicies: User.IsInRole("ProjectManager"),
                cancellationToken);
            return HandleResult(result);
        }

        [HttpPost("promote")]
        [Authorize(Roles = "ProjectManager")]
        public async Task<ActionResult> Promote(
            [FromBody] PromoteProjectPolicyRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _projectPolicyService.PromoteAsync(request, cancellationToken);
            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            return HandleResult(result);
        }

        [HttpGet("{projectId:guid}")]
        public async Task<ActionResult> GetPolicies(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            var result = await _projectPolicyService.GetPoliciesAsync(projectId, cancellationToken);
            return HandleResult(result);
        }

        [HttpDelete("{documentId:guid}")]
        [Authorize(Roles = "ProjectManager")]
        public async Task<ActionResult> DeletePolicy(
            Guid documentId,
            [FromQuery] Guid projectId,
            CancellationToken cancellationToken)
        {
            var result = await _projectPolicyService.DeleteAsync(documentId, projectId, cancellationToken);
            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            return HandleResult(result);
        }
    }
}
