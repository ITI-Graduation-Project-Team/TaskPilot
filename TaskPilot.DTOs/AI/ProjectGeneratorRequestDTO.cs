namespace TaskPilot.DTOs.AI
{
    /// <summary>
    /// Form data sent by the Project Manager to trigger AI project generation.
    /// File inputs (audio, document) are received directly as IFormFile parameters
    /// in the controller action — keeping this DTO framework-agnostic.
    /// At least one of <see cref="TextRequirements"/> plus the controller's file params must supply content.
    /// </summary>
    public class ProjectGeneratorRequestDTO
    {
        /// <summary>Free-text description of the project requirements.</summary>
        public string? TextRequirements { get; set; }

        /// <summary>The company this project belongs to.</summary>
        public Guid CompanyId { get; set; }

        /// <summary>The Project Manager who will own this project.</summary>
        public Guid ManagerId { get; set; }
    }
}
