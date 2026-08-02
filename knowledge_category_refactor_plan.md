# Implementation Plan: KnowledgeCollectionType Display Name Mapping

## Problem Analysis

The current `KnowledgeOrchestrator.AskAsync` builds `KnowledgeSource` objects using `chunk.Category` (a `DocumentCategory` enum like `Uncategorized`). When `CompanyPolicyService.AskAsync` projects that into `CompanyPolicySourceDto`, it calls `.ToString()` on that enum, producing the raw enum name (e.g., `"Uncategorized"`).

The fix: the **collection type** (e.g., `CompanyPolicies`) — not the chunk-level document category — is the right semantic label for the API response. We need a centralized mapper and must thread `collectionType` through to wherever `KnowledgeSource` is projected into a DTO.

---

## Scope of Changes (4 files, 1 new file)

| # | Action | File | Layer |
|---|--------|------|-------|
| 1 | **CREATE** | `TaskPilot.Models/Extensions/KnowledgeCollectionTypeExtensions.cs` | Domain |
| 2 | **UPDATE** enum | `TaskPilot.Models/Enums/KnowledgeCollectionType.cs` | Domain |
| 3 | **UPDATE** model | `TaskPilot.AI/Models/RAG/KnowledgeSource.cs` | AI |
| 4 | **UPDATE** orchestrator | `TaskPilot.AI/Orchestrators/KnowledgeOrchestrator.cs` | AI |
| 5 | **UPDATE** service | `TaskPilot.Services/CompanyPolicyService.cs` | Services |

> **No changes** to: chunk metadata, vector payloads, ingestion pipeline, QdrantVectorStore, or any entity/document models.

---

## Step 1 — Extend the `KnowledgeCollectionType` enum

The enum currently only has `ProjectPolicies` and `CompanyPolicies`. The task requires adding `Requirements`, `Vision`, and `GeneralKnowledge` for future-proofing.

> [!IMPORTANT]
> Adding enum values is non-breaking. The `QdrantVectorStore.GetCollectionName` has a `_ => throw` fallback — we must add the new values there too OR leave the enum as-is for now and only add to the mapper. Since the task says "future-proof the mapping," we add to **both** the enum and the mapper, but leave Qdrant collection creation opt-in (no automatic Qdrant collection created for unregistered types).

**File:** `TaskPilot.Models/Enums/KnowledgeCollectionType.cs`

```csharp
namespace TaskPilot.Models.Enums
{
    public enum KnowledgeCollectionType
    {
        ProjectPolicies,
        CompanyPolicies,
        Requirements,
        Vision,
        GeneralKnowledge
    }
}
```

---

## Step 2 — CREATE the centralized extension method

**New file:** `TaskPilot.Models/Extensions/KnowledgeCollectionTypeExtensions.cs`

```csharp
using System;
using TaskPilot.Models.Enums;

namespace TaskPilot.Models.Extensions
{
    /// <summary>
    /// Centralized display-name mapping for <see cref="KnowledgeCollectionType"/>.
    /// All human-readable category strings for API responses must originate here.
    /// To add a new collection: (1) add the enum value, (2) add a case below.
    /// </summary>
    public static class KnowledgeCollectionTypeExtensions
    {
        public static string ToDisplayName(this KnowledgeCollectionType type) => type switch
        {
            KnowledgeCollectionType.CompanyPolicies  => "Company Policy",
            KnowledgeCollectionType.ProjectPolicies  => "Project Policy",
            KnowledgeCollectionType.Requirements     => "Requirement Document",
            KnowledgeCollectionType.Vision           => "Vision Document",
            KnowledgeCollectionType.GeneralKnowledge => "Knowledge Base",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No display name registered for this KnowledgeCollectionType.")
        };
    }
}
```

**Why in `TaskPilot.Models`?**
- `KnowledgeCollectionType` lives in `TaskPilot.Models.Enums`.
- Extension methods on a type should live in the same assembly as the type, or in a higher-level assembly that already depends on it.
- `TaskPilot.Models` has no dependency on AI or Services layers, making it the clean home. All other layers already reference `TaskPilot.Models`.

---

## Step 3 — Add `CollectionDisplayName` to `KnowledgeSource`

`KnowledgeSource` is the internal domain model that flows from `KnowledgeOrchestrator` up to callers. We add a `CollectionDisplayName` string so the orchestrator stamps it once and all DTO projections downstream can read it without re-deriving.

**File:** `TaskPilot.AI/Models/RAG/KnowledgeSource.cs`

```csharp
using System;
using TaskPilot.AI.Enums;

namespace TaskPilot.AI.Models.RAG
{
    public class KnowledgeSource
    {
        public Guid DocumentId { get; set; }

        public Guid ChunkId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public DocumentCategory Category { get; set; }

        /// <summary>
        /// Human-readable display name derived from the <see cref="KnowledgeCollectionType"/>
        /// used during retrieval. Populated by <see cref="KnowledgeOrchestrator"/>.
        /// </summary>
        public string CollectionDisplayName { get; set; } = string.Empty;
    }
}
```

---

## Step 4 — Stamp `CollectionDisplayName` in `KnowledgeOrchestrator`

`KnowledgeOrchestrator.AskAsync` already receives `collectionType` as a parameter. We call `.ToDisplayName()` once here and assign it to each `KnowledgeSource`.

**File:** `TaskPilot.AI/Orchestrators/KnowledgeOrchestrator.cs`

Add using:
```csharp
using TaskPilot.Models.Extensions;
```

Change the source-building loop from:
```csharp
sources.Add(new KnowledgeSource
{
    DocumentId = chunk.DocumentId,
    ChunkId = chunk.Id,
    FileName = chunk.SourceFile,
    Category = chunk.Category
});
```

To:
```csharp
sources.Add(new KnowledgeSource
{
    DocumentId = chunk.DocumentId,
    ChunkId = chunk.Id,
    FileName = chunk.SourceFile,
    Category = chunk.Category,
    CollectionDisplayName = collectionType.ToDisplayName()
});
```

This is the **single point of truth** — the display name is derived from the collection context, not from chunk metadata.

---

## Step 5 — Update `CompanyPolicyService` DTO projection

`CompanyPolicyService.AskAsync` currently projects:
```csharp
Category = s.Category.ToString()   // ← produces "Uncategorized"
```

Change to:
```csharp
Category = s.CollectionDisplayName  // ← produces "Company Policy"
```

**File:** `TaskPilot.Services/CompanyPolicyService.cs`

```csharp
Sources = result.Value.Sources.Select(s => new CompanyPolicySourceDto
{
    FileName = s.FileName,
    Category = s.CollectionDisplayName   // ← use pre-stamped display name
}).ToList()
```

---

## Data Flow After Refactor

```
HTTP Request
    │
    ▼
KnowledgeController / CompanyPolicyController
    │   collectionType = KnowledgeCollectionType.CompanyPolicies
    ▼
KnowledgeOrchestrator.AskAsync(collectionType, ...)
    │   collectionType.ToDisplayName() → "Company Policy"
    │   KnowledgeSource { CollectionDisplayName = "Company Policy" }
    ▼
CompanyPolicyService (or KnowledgeController)
    │   s.CollectionDisplayName → "Company Policy"
    ▼
CompanyPolicySourceDto { Category = "Company Policy" }
    ▼
HTTP Response: { "category": "Company Policy" }
```

---

## Mapping Reference Table

| `KnowledgeCollectionType` | `ToDisplayName()` result |
|---|---|
| `CompanyPolicies` | `"Company Policy"` |
| `ProjectPolicies` | `"Project Policy"` |
| `Requirements` | `"Requirement Document"` |
| `Vision` | `"Vision Document"` |
| `GeneralKnowledge` | `"Knowledge Base"` |

---

## QdrantVectorStore Compatibility

The new enum values (`Requirements`, `Vision`, `GeneralKnowledge`) are added to `KnowledgeCollectionType`. The `QdrantVectorStore.GetCollectionName` currently throws `ArgumentOutOfRangeException` for unknown values. 

> [!WARNING]
> We must also add the new enum values to `QdrantVectorStore.GetCollectionName` to prevent a runtime exception if those collections are ever used. They can point to configurable collection names, exactly like the existing two.

**File:** `TaskPilot.AI/Services/QdrantVectorStore.cs` — `GetCollectionName` method:

```csharp
private string GetCollectionName(KnowledgeCollectionType type)
{
    return type switch
    {
        KnowledgeCollectionType.ProjectPolicies  => string.IsNullOrWhiteSpace(_options.Collections.ProjectPolicies)  ? "taskpilot_project_policies"  : _options.Collections.ProjectPolicies,
        KnowledgeCollectionType.CompanyPolicies  => string.IsNullOrWhiteSpace(_options.Collections.CompanyPolicies)  ? "taskpilot_company_policies"  : _options.Collections.CompanyPolicies,
        KnowledgeCollectionType.Requirements     => "taskpilot_requirements",
        KnowledgeCollectionType.Vision           => "taskpilot_vision",
        KnowledgeCollectionType.GeneralKnowledge => "taskpilot_general_knowledge",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
```

> This is an infrastructure concern (Qdrant collection naming), not a display-name concern. It stays in `QdrantVectorStore`.

---

## Summary of Files Changed

| File | Change Type | What Changes |
|------|-------------|--------------|
| `TaskPilot.Models/Enums/KnowledgeCollectionType.cs` | Edit | Add 3 new enum values |
| `TaskPilot.Models/Extensions/KnowledgeCollectionTypeExtensions.cs` | **New** | Centralized `ToDisplayName()` extension |
| `TaskPilot.AI/Models/RAG/KnowledgeSource.cs` | Edit | Add `CollectionDisplayName` property |
| `TaskPilot.AI/Orchestrators/KnowledgeOrchestrator.cs` | Edit | Stamp `CollectionDisplayName` from `collectionType` |
| `TaskPilot.Services/CompanyPolicyService.cs` | Edit | Use `s.CollectionDisplayName` instead of `s.Category.ToString()` |
| `TaskPilot.AI/Services/QdrantVectorStore.cs` | Edit | Add 3 new cases to `GetCollectionName` |

**Total: 1 new file, 5 edited files. Zero breaking changes.**
