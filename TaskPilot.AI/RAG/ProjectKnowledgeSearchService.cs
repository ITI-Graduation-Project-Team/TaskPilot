using System.Text.RegularExpressions;
using TaskPilot.AI.Models.ContextAdvisor;
using TaskPilot.AI.Persistence.Interfaces;

namespace TaskPilot.AI.RAG
{
    public class ProjectKnowledgeSearchService : IProjectKnowledgeSearchService
    {
        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "with", "from", "that", "this", "are", "was", "were",
            "has", "have", "had", "about", "into", "what", "when", "where", "which",
            "how", "why", "can", "could", "should", "would", "will", "task", "project"
        };

        private readonly IDocumentStore _documentStore;

        public ProjectKnowledgeSearchService(IDocumentStore documentStore)
        {
            _documentStore = documentStore;
        }

        public async Task<List<RetrievedKnowledgeChunk>> SearchAsync(
            Guid? projectId,
            string query,
            int topK,
            CancellationToken cancellationToken = default)
        {
            var chunks =
                await _documentStore
                    .GetAvailableChunksAsync(projectId, cancellationToken);

            if (!chunks.Any())
            {
                return new List<RetrievedKnowledgeChunk>();
            }

            var queryTokens =
                Tokenize(query)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var scoredChunks = new List<RetrievedKnowledgeChunk>();

            foreach (var chunk in chunks)
            {
                var document =
                    await _documentStore
                        .GetDocumentAsync(chunk.DocumentId, cancellationToken);

                if (document is null)
                {
                    continue;
                }

                var score =
                    Score(query, queryTokens, chunk.Content);

                scoredChunks.Add(
                    new RetrievedKnowledgeChunk
                    {
                        ChunkId = chunk.Id,
                        DocumentId = chunk.DocumentId,
                        ProjectId = chunk.ProjectId,
                        FileName = document.FileName,
                        ChunkIndex = chunk.ChunkIndex,
                        Content = chunk.Content,
                        SourceUrl = string.IsNullOrWhiteSpace(document.CloudinaryUrl)
                            ? null
                            : document.CloudinaryUrl,
                        Score = score
                    });
            }

            var ordered =
                scoredChunks
                    .OrderByDescending(chunk => chunk.Score)
                    .ThenByDescending(chunk => chunk.ChunkIndex)
                    .Take(Math.Clamp(topK, 1, 12))
                    .ToList();

            return ordered.Any(chunk => chunk.Score > 0)
                ? ordered
                : scoredChunks
                    .OrderByDescending(chunk => chunk.ChunkIndex)
                    .Take(Math.Clamp(topK, 1, 12))
                    .ToList();
        }

        private static double Score(
            string query,
            HashSet<string> queryTokens,
            string content)
        {
            if (!queryTokens.Any() || string.IsNullOrWhiteSpace(content))
            {
                return 0;
            }

            var contentTokens =
                Tokenize(content)
                    .ToList();

            if (!contentTokens.Any())
            {
                return 0;
            }

            var contentTokenSet =
                contentTokens
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var overlap =
                queryTokens
                    .Count(contentTokenSet.Contains);

            var score =
                overlap / Math.Sqrt(contentTokenSet.Count);

            foreach (var phrase in ExtractPhrases(query))
            {
                if (content.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                {
                    score += 2.0;
                }
            }

            return score;
        }

        private static IEnumerable<string> Tokenize(string text)
        {
            return Regex
                .Matches(text.ToLowerInvariant(), "[a-z0-9][a-z0-9_\\-]{2,}")
                .Select(match => match.Value)
                .Where(token => !StopWords.Contains(token));
        }

        private static IEnumerable<string> ExtractPhrases(string text)
        {
            var tokens =
                Tokenize(text)
                    .ToList();

            for (var index = 0; index < tokens.Count - 1; index++)
            {
                yield return $"{tokens[index]} {tokens[index + 1]}";
            }
        }
    }
}
