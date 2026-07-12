namespace TaskPilot.AI.Options
{
    public class QdrantOptions
    {
        public string Url { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public QdrantCollections Collections { get; set; } = new();
    }

    public class QdrantCollections
    {
        public string ProjectPolicies { get; set; } = string.Empty;
        public string CompanyPolicies { get; set; } = string.Empty;
    }
}
