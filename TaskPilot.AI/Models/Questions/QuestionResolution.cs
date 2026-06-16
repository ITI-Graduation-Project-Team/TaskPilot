namespace TaskPilot.AI.Models.Questions
{
    public class QuestionResolution
    {
        public Guid QuestionId
        {
            get;
            set;
        }

        public bool IsAnswered
        {
            get;
            set;
        }

        public string ExtractedAnswer
        {
            get;
            set;
        }
        =
            string.Empty;
    }
}
