namespace TaskPilot.DTOs.AI
{
    /// <summary>
    /// Represents the AI-generated project draft returned to the Project Manager for review.
    /// The PM can inspect / tweak the values before confirming via POST /api/aiproject/confirm.
    /// Nothing is persisted until confirmation.
    /// </summary>
    public class GeneratedProjectDTO
    {
        /// <summary>AI-suggested project name (English).</summary>
        public string NameEn { get; set; } = string.Empty;

        /// <summary>AI-suggested project name (Arabic).</summary>
        public string NameAr { get; set; } = string.Empty;

        /// <summary>AI-generated project description (English).</summary>
        public string? DescriptionEn { get; set; }

        /// <summary>AI-generated project description (Arabic).</summary>
        public string? DescriptionAr { get; set; }

        /// <summary>The company this project will belong to — echoed from the request.</summary>
        public Guid CompanyId { get; set; }

        /// <summary>The Project Manager who will own this project — echoed from the request.</summary>
        public Guid ManagerId { get; set; }
    }
}
