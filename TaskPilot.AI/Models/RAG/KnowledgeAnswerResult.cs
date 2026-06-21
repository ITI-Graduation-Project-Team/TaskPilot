using System.Collections.Generic;

namespace TaskPilot.AI.Models.RAG
{
    public class KnowledgeAnswerResult
    {
        public string Answer { get; set; } = string.Empty;

        public List<KnowledgeSource> Sources { get; set; } = [];
    }
}
