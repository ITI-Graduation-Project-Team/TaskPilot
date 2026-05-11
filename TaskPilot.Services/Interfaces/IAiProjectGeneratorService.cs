using TaskPilot.DTOs.AI;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    /// <summary>
    /// Generates a project draft from natural-language requirements using OpenAI GPT.
    /// Does NOT persist anything — returns a <see cref="GeneratedProjectDTO"/> for PM review.
    /// </summary>
    public interface IAiProjectGeneratorService
    {
        /// <summary>
        /// Calls OpenAI to produce a structured project draft from free-text requirements.
        /// </summary>
        /// <param name="requirements">Combined requirements text (may originate from text, audio, or document).</param>
        /// <param name="companyId">Company the project will belong to.</param>
        /// <param name="managerId">Project Manager who will own the project.</param>
        Task<Result<GeneratedProjectDTO>> GenerateProjectAsync(
            string requirements,
            Guid companyId,
            Guid managerId);

        /// <summary>
        /// Validates the PM-approved draft and persists it as a new <see cref="TaskPilot.Models.Entities.Project"/>.
        /// The controller is responsible for calling SaveChangesAsync after this method succeeds.
        /// </summary>
        /// <param name="dto">The (possibly edited) draft returned by <see cref="GenerateProjectAsync"/>.</param>
        Task<Result<Guid>> ConfirmProjectAsync(GeneratedProjectDTO dto);
    }
}
