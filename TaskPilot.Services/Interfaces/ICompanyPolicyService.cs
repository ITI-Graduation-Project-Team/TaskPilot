using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.AI.CompanyPolicies;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface ICompanyPolicyService
    {
        Task<Result<UploadCompanyPolicyResponse>> UploadAsync(UploadCompanyPolicyRequest request, Func<CancellationToken, Task> saveChangesAsync, CancellationToken cancellationToken = default);
        Task<Result<CompanyPolicyAnswerResponse>> AskAsync(CompanyPolicyQuestionRequest request, CancellationToken cancellationToken = default);
        Task<Result<List<CompanyPolicyDocumentDto>>> GetDocumentsAsync(Guid companyId, CancellationToken cancellationToken = default);
        Task<Result> DeleteDocumentAsync(Guid companyId, Guid documentId, CancellationToken cancellationToken = default);
    }
}
