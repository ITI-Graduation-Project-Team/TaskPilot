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
        /// On first call, pass <paramref name="chatId"/> as null to start a new session.
        /// On follow-up calls, pass the <c>ChatId</c> returned by the previous response to
        /// continue the conversation — only send the new answer, not the full history.
        /// </summary>
        /// <param name="newMessage">The user's new message (requirements or answers to clarification questions).</param>
        /// <param name="companyId">Company the project will belong to.</param>
        /// <param name="managerId">Project Manager who will own the project.</param>
        /// <param name="chatId">Existing session ID, or null to start fresh.</param>
        Task<Result<GeneratedProjectDTO>> GenerateProjectAsync(
            string newMessage,
            Guid companyId,
            Guid managerId,
            string? chatId = null);


        /// <summary>
        /// Validates the PM-approved draft and persists it as a new <see cref="TaskPilot.Models.Entities.Project"/>.
        /// The controller is responsible for calling SaveChangesAsync after this method succeeds.
        /// </summary>
        /// <param name="dto">The (possibly edited) draft returned by <see cref="GenerateProjectAsync"/>.</param>
        Task<Result<Guid>> ConfirmProjectAsync(GeneratedProjectDTO dto);
    }
}
