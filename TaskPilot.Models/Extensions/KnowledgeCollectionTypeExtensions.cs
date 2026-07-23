using System;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Extensions
{
    /// <summary>
    /// Centralized display-name mapping for <see cref="KnowledgeCollectionType"/>.
    /// All human-readable category strings in API responses must originate here.
    /// </summary>
    public static class KnowledgeCollectionTypeExtensions
    {
        public static string ToDisplayName(this KnowledgeCollectionType type) => type switch
        {
            KnowledgeCollectionType.CompanyPolicies => "Company Policy",
            KnowledgeCollectionType.ProjectPolicies => "Project Policy",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type,
                     "No display name registered for this KnowledgeCollectionType.")
        };
    }
}
