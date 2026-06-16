using TaskPilot.AI.Models.Ingestion;

namespace TaskPilot.AI.Models.Session
{
    public class SessionKnowledgeContext
    {
        public List<Guid>
            DocumentIds
        {
            get;
            set;
        }
        =
            new();

        public List<IngestedDocument>
            Documents
        {
            get;
            set;
        }
        =
            new();

        //public KnowledgeCoverageReport
        //    Coverage
        //{
        //    get;
        //    set;
        //}
        //=
        //    new();
    }
}
