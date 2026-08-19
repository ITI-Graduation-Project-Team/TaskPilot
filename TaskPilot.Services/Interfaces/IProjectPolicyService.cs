using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.AI.ProjectPolicies;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IProjectPolicyService
    {
        Task<Result<UploadProjectPolicyResponse>> IngestAsync(
            IngestProjectPolicyRequest request,
            Func<CancellationToken, Task> saveChangesAsync,
            CancellationToken cancellationToken = default);

        Task<Result<ProjectPolicyAnswerResponse>> AskAsync(
            ProjectPolicyQuestionRequest request,
            bool canUploadPolicies,
            CancellationToken cancellationToken = default);

        Task<Result> PromoteAsync(
            PromoteProjectPolicyRequest request,
            CancellationToken cancellationToken = default);

        Task<Result<List<ProjectPolicyDocumentDto>>> GetPoliciesAsync(
            Guid projectId,
            CancellationToken cancellationToken = default);

        Task<Result> DeleteAsync(
            Guid documentId,
            Guid projectId,
            CancellationToken cancellationToken = default);
    }
}
