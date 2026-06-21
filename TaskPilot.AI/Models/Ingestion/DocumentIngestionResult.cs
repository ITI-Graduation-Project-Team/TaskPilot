using TaskPilot.AI.Enums;

namespace TaskPilot.AI.Models.Ingestion
{
    public class DocumentIngestionResult
    {
        public bool Success
        {
            get;
            set;
        }

        public Guid DocumentId
        {
            get;
            set;
        }

        public DocumentCategory
        Category
        {
            get;
            set;
        }
        =
        DocumentCategory
        .Uncategorized;

        public int ChunksCreated
        {
            get;
            set;
        }

        public int QuestionsAutoResolved
        {
            get;
            set;
        }

        public string Message
        {
            get;
            set;
        }
        =
            string.Empty;
    }
}