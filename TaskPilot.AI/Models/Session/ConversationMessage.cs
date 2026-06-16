namespace TaskPilot.AI.Models.Session
{
    public class ConversationMessage
    {
        public string Role
        {
            get;
            set;
        }
        =
            string.Empty;

        public string Message
        {
            get;
            set;
        }
        =
            string.Empty;

        public DateTime Timestamp
        {
            get;
            set;
        }
        =
            DateTime.UtcNow;
    }
}
