namespace TaskPilot.DTOs.AI
{
    /// <summary>
    /// Form data sent by the Project Manager to trigger or continue an AI project generation session.
    /// File inputs (audio, document) are received directly as IFormFile parameters
    /// in the controller action — keeping this DTO framework-agnostic.
    /// </summary>
    public class ProjectGeneratorRequestDTO
    {
        /// <summary>
        /// An existing OpenAI Thread ID returned by a previous /generate call (e.g. "thread_abc123").
        /// When provided, the message is appended to the existing thread on OpenAI's side —
        /// no server-side state is maintained. Leave null to start a new thread.
        /// </summary>
        public string? ChatId { get; set; }

        /// <summary>Free-text requirements (first call) or answers to clarification questions (follow-up calls).</summary>
        public string? TextRequirements { get; set; }

        /// <summary>The company this project belongs to.</summary>
        public Guid CompanyId { get; set; }

        /// <summary>The Project Manager who will own this project.</summary>
        public Guid ManagerId { get; set; }
    }
}
