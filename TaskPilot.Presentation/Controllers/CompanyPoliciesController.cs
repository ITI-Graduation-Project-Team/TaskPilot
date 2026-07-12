using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.AI.CompanyPolicies;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/company-policies")]
    public class CompanyPoliciesController : ApiControllerBase
    {
        private readonly ICompanyPolicyService _companyPolicyService;
        private readonly IUnitOfWork _unitOfWork;

        public CompanyPoliciesController(ICompanyPolicyService companyPolicyService, IUnitOfWork unitOfWork)
        {
            _companyPolicyService = companyPolicyService;
            _unitOfWork = unitOfWork;
        }

        [HttpPost("upload")]
        public async Task<ActionResult> Upload(
            [FromForm] UploadCompanyPolicyRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _companyPolicyService.UploadAsync(
                request,
                async ct => await _unitOfWork.SaveChangesAsync(ct),
                cancellationToken);

            return HandleResult(result);
        }

        [HttpPost("ask")]
        public async Task<ActionResult> Ask(
            [FromBody] CompanyPolicyQuestionRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _companyPolicyService.AskAsync(request, cancellationToken);
            return HandleResult(result);
        }

        [HttpGet("documents")]
        public async Task<ActionResult> GetDocuments(
            [FromQuery] Guid companyId,
            CancellationToken cancellationToken)
        {
            var result = await _companyPolicyService.GetDocumentsAsync(companyId, cancellationToken);
            return HandleResult(result);
        }

        [HttpDelete("documents/{documentId}")]
        public async Task<ActionResult> DeleteDocument(
            [FromQuery] Guid companyId,
            Guid documentId,
            CancellationToken cancellationToken)
        {
            var result = await _companyPolicyService.DeleteDocumentAsync(companyId, documentId, cancellationToken);
            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            return HandleResult(result);
        }
    }
}
