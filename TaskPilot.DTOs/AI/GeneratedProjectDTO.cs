namespace TaskPilot.DTOs.AI
{
    /// <summary>
    /// Returned by POST /api/aiproject/generate.
    ///
    /// Two possible states:
    ///   1. <b>Clarification needed</b> — <see cref="ClarificationQuestions"/> is populated,
    ///      project fields are empty. The PM should answer the questions and call /generate again.
    ///   2. <b>Draft ready</b> — <see cref="ClarificationQuestions"/> is empty,
    ///      project fields are filled. The PM reviews and calls /confirm.
    /// </summary>
    public class GeneratedProjectDTO
    {
        // ── Session ───────────────────────────────────────────────────────────────

        /// <summary>
        /// The OpenAI Thread ID for this conversation (e.g. "thread_abc123").
        /// Pass this back on every subsequent /generate call — OpenAI stores the
        /// full conversation history on their side; the PM only sends the new message.
        /// </summary>
        public string ChatId { get; set; } = string.Empty;

        // ── Clarification (populated when AI needs more info) ─────────────────────


        /// <summary>
        /// Questions the AI needs answered before it can produce a reliable project draft.
        /// Empty when the requirements were clear enough to generate a full draft.
        /// </summary>
        public List<string> ClarificationQuestions { get; set; } = new();

        // ── Project draft (populated when requirements are sufficient) ────────────

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

        /// <summary>True when the AI returned questions instead of a draft.</summary>
        public bool NeedsClarification => ClarificationQuestions.Count > 0;
    }
}
