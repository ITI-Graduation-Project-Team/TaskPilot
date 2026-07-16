using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Services.Requirements
{
    public class RequirementConsolidationEngine
    {
        private readonly IEmbeddingService _embeddingService;
        private const float SimilarityThreshold = 0.85f;

        public RequirementConsolidationEngine(IEmbeddingService embeddingService)
        {
            _embeddingService = embeddingService;
        }

        public async Task ConsolidateAsync(
            RequirementSession session,
            List<RequirementIdentity> newRequirements,
            CancellationToken cancellationToken = default)
        {
            foreach (var req in newRequirements)
            {
                if (req.Embedding == null || req.Embedding.Length == 0)
                {
                    req.Embedding = await _embeddingService.GenerateEmbeddingAsync(req.OriginalText, cancellationToken);
                }

                req.NormalizedText = NormalizeText(req.OriginalText);

                // Find best match in the same category
                var bestMatch = session.ConsolidatedKnowledgeBase
                    .Where(x => string.Equals(x.Category, req.Category, StringComparison.OrdinalIgnoreCase))
                    .Select(x => new { Req = x, Similarity = CosineSimilarity(x.Embedding, req.Embedding) })
                    .OrderByDescending(x => x.Similarity)
                    .FirstOrDefault();

                if (bestMatch != null && bestMatch.Similarity >= SimilarityThreshold)
                {
                    // Merge
                    var existing = bestMatch.Req;
                    existing.Version++;
                    existing.UpdatedAt = DateTime.UtcNow;
                    
                    if (req.Confidence > existing.Confidence)
                    {
                        existing.OriginalText = req.OriginalText;
                        existing.NormalizedText = req.NormalizedText;
                        existing.Confidence = req.Confidence;
                    }
                    
                    if (req.IsConflicting)
                    {
                        existing.IsConflicting = true;
                        existing.ConflictReason = req.ConflictReason;
                    }

                    foreach (var src in req.Sources)
                        if (!existing.Sources.Contains(src)) existing.Sources.Add(src);

                    foreach (var ev in req.Evidence)
                        if (!existing.Evidence.Contains(ev)) existing.Evidence.Add(ev);
                }
                else
                {
                    // Insert
                    session.ConsolidatedKnowledgeBase.Add(req);
                }
            }

            // Sync back to ExtractedRequirements for backward compatibility
            SyncToExtractedRequirements(session);
        }

        private void SyncToExtractedRequirements(RequirementSession session)
        {
            session.Requirements.BusinessRequirements = GetByCategory(session, "BusinessGoals");
            session.Requirements.TechnicalRequirements = GetByCategory(session, "Technical");
            session.Requirements.Constraints = GetByCategory(session, "Constraints");
            session.Requirements.Integrations = GetByCategory(session, "Integrations");
            session.Requirements.ScaleRequirements = GetByCategory(session, "Scale");
        }

        private List<string> GetByCategory(RequirementSession session, string category)
        {
            return session.ConsolidatedKnowledgeBase
                .Where(r => string.Equals(r.Category, category, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.OriginalText)
                .ToList();
        }

        private string NormalizeText(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return input.Trim().ToLowerInvariant().Replace("  ", " ");
        }

        private static float CosineSimilarity(float[] vector1, float[] vector2)
        {
            if (vector1 == null || vector2 == null || vector1.Length != vector2.Length || vector1.Length == 0) 
                return 0f;
                
            float dotProduct = 0f;
            float norm1 = 0f;
            float norm2 = 0f;
            for (int i = 0; i < vector1.Length; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                norm1 += vector1[i] * vector1[i];
                norm2 += vector2[i] * vector2[i];
            }
            if (norm1 == 0f || norm2 == 0f) return 0f;
            return dotProduct / (float)(Math.Sqrt(norm1) * Math.Sqrt(norm2));
        }
    }
}
