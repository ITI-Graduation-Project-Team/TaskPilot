using TaskPilot.AI.Enums;

namespace TaskPilot.AI.Models.Questions
{
    public class ClarificationQuestion
    {
        public Guid Id
        {
            get;
            set;
        }
        =
            Guid.NewGuid();

        public string Question
        {
            get;
            set;
        }
        =
            string.Empty;

        public QuestionCategory
        Category
        {
            get;
            set;
        }
        =
          QuestionCategory
        .General;

        public QuestionPriority
            Priority
        {
            get;
            set;
        }
        =
            QuestionPriority
                .Medium;

        public bool IsAnswered
        {
            get;
            set;
        }

        public string?
            Answer
        {
            get;
            set;
        }

        public DateTime?
            AnsweredAt
        {
            get;
            set;
        }

        public string?
            AnsweredFromSource
        {
            get;
            set;
        }
    }
}
