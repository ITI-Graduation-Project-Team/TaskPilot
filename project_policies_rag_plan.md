# Project Policies RAG — Implementation Plan

---

## 1. Architecture Review

TaskPilot follows **Clean Architecture** with these layers:

| Layer | Project | Responsibility |
|---|---|---|
| Domain/Models | `TaskPilot.Models` | Entities, Enums, Errors, Results |
| Data | `TaskPilot.Data` | EF Core, IRepository, IUnitOfWork |
| AI | `TaskPilot.AI` | Agents, Orchestrators, VectorStore |
| Services | `TaskPilot.Services` | Business logic, orchestration |
| DTOs | `TaskPilot.DTOs` | API contracts |
| Presentation | `TaskPilot.Presentation` | Controllers, Filters, Middleware |
| Tests | `TaskPilot.Tests` | Unit + integration tests |

**Key patterns in use:**
- Result Pattern (`Result<T>` / `Result`)
- UnitOfWork — called only in Controllers
- `HandleResult` — only place HTTP status codes are decided
- `IRepository<T>` — generic, no redesign
- Errors via static error catalogues (`KnowledgeErrors`, `CommonErrors`, `ProjectErrors`)
- Localization via `ILocalizationService`
- Semaphore-based concurrency guard per tenant
- Deterministic `DocumentId` via MD5 hash for duplicate prevention
- `KnowledgeCollectionType` enum routes to correct Qdrant collection
- `IVectorStore.PromoteKnowledgeAsync` already exists — updates payload only, never re-embeds

**Already in place for Project Policies:**
- `KnowledgeCollectionType.ProjectPolicies` enum value ✓
- `GetCollectionName` maps it to `taskpilot_project_policies` ✓
- `KnowledgeCollectionTypeExtensions.ToDisplayName` maps it to `"Project Policy"` ✓
- `Policy` entity has `ProjectId`, `PolicyScope.Project` ✓
- `IVectorStore` has `PromoteKnowledgeAsync` ✓
- `KnowledgeChunk` has `RequirementSessionId`, `ProjectId` fields ✓
- Qdrant payload indexes for `RequirementSessionId` and `ProjectId` already created ✓

---

## 2. Components Reused (Zero Modification)

| Component | Reused As-Is |
|---|---|
| `KnowledgeOrchestrator` | Drives Ask flow |
| `KnowledgeRetrievalAgent` | Drives retrieval with filter |
| `KnowledgeAnswerAgent` | Generates final answer |
| `ChunkingAgent` | Chunks extracted text |
| `DocumentCategorizationAgent` | Categorizes document |
| `IVectorStore` / `QdrantVectorStore` | Upsert, Search, Delete, Promote |
| `IDocumentTextExtractor` | Extracts text from file |
| `IFileStorageService` | Cloudinary upload |
| `IRepository<Policy>` | Stores policy records |
| `IRepository<Project>` | Validates project existence |
| `KnowledgeCollectionType.ProjectPolicies` | Routes to correct collection |
| `KnowledgeErrors.MissingTenantIsolation` | Tenant guard error |
| `CommonErrors` | NotFound, InvalidInput, Conflict |
| `ProjectErrors.NotFound` | Project not found |
| `ApiControllerBase.HandleResult` | HTTP mapping |
| `Policy` entity | Stores project policy records |
| `PolicyScope.Project` | Scope discriminator |

---

## 3. New Controller

**File:** `TaskPilot.Presentation/Controllers/ProjectPoliciesController.cs`

- Route: `api/project-policies`
- Inherits: `ApiControllerBase`
- Injects: `IProjectPolicyService`, `IUnitOfWork`
- Endpoints:
  - `POST /upload` — multipart/form-data
  - `POST /ask` — JSON body
  - `POST /promote` — JSON body

`IUnitOfWork.SaveChangesAsync` called **in the controller** on Upload success (matching Company pattern exactly).

---

## 4. New Service

**Interface:** `TaskPilot.Services/Interfaces/IProjectPolicyService.cs`

```csharp
public interface IProjectPolicyService
{
    Task<Result<UploadProjectPolicyResponse>> IngestAsync(
        IngestProjectPolicyRequest request,
        Func<CancellationToken, Task> saveChangesAsync,
        CancellationToken cancellationToken = default);

    Task<Result<ProjectPolicyAnswerResponse>> AskAsync(
        ProjectPolicyQuestionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> PromoteAsync(
        PromoteProjectPolicyRequest request,
        CancellationToken cancellationToken = default);
}
```

**Implementation:** `TaskPilot.Services/ProjectPolicyService.cs`

Constructor injects:
- `IEnumerable<IDocumentTextExtractor>` extractors
- `DocumentCategorizationAgent` categorizationAgent
- `ChunkingAgent` chunkingAgent
- `IVectorStore` vectorStore
- `KnowledgeOrchestrator` knowledgeOrchestrator
- `IRepository<Policy>` policyRepository
- `IRepository<Project>` projectRepository
- `IUnitOfWork` unitOfWork
- `IFileStorageService` fileStorage
- `ILogger<ProjectPolicyService>` logger

Holds one static `ConcurrentDictionary<Guid, SemaphoreSlim>` for per-tenant concurrency (same pattern as `CompanyPolicyService`).

---

## 5. Required DTOs

**Namespace:** `TaskPilot.DTOs.AI.ProjectPolicies`

### `UploadProjectPolicyRequest.cs` (form-model)
```csharp
public class UploadProjectPolicyRequest
{
    public Guid? ProjectId { get; set; }
    public Guid? RequirementSessionId { get; set; }
    [Required]
    public IFormFile File { get; set; } = null!;
}
```

### `IngestProjectPolicyRequest.cs` (service-model)
```csharp
public class IngestProjectPolicyRequest
{
    public Guid? ProjectId { get; set; }
    public Guid? RequirementSessionId { get; set; }
    public IFormFile? File { get; set; }
    public string? TitleEn { get; set; }
    public string? DocumentUrl { get; set; }
    public string? CloudinaryPublicId { get; set; }
    public bool SkipCloudUpload { get; set; }
}
```

### `UploadProjectPolicyResponse.cs`
```csharp
public class UploadProjectPolicyResponse
{
    public Guid DocumentId { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ChunksCreated { get; set; }
}
```

### `ProjectPolicyQuestionRequest.cs`
```csharp
public class ProjectPolicyQuestionRequest
{
    public Guid? ProjectId { get; set; }
    public Guid? RequirementSessionId { get; set; }
    [Required, MinLength(3)]
    public string Question { get; set; } = string.Empty;
}
```

### `ProjectPolicyAnswerResponse.cs`
```csharp
public class ProjectPolicyAnswerResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<ProjectPolicySourceDto> Sources { get; set; } = new();
}

public class ProjectPolicySourceDto
{
    public string FileName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
```

### `PromoteProjectPolicyRequest.cs`
```csharp
public class PromoteProjectPolicyRequest
{
    [Required]
    public Guid RequirementSessionId { get; set; }
    [Required]
    public Guid ProjectId { get; set; }
}
```

---

## 6. Repository Changes

**None required.**

- `IRepository<Policy>` already supports all operations needed.
- `IRepository<Project>` already supports `GetByIdAsync`.
- No new repository interfaces or implementations.

The `Policy` entity already has:
- `ProjectId` (nullable Guid)
- `RequirementSessionId` — **not present yet, needs adding** (see Section 8).

---

## 7. AI Layer Changes

**None to existing agents or orchestrators.**

The `KnowledgeOrchestrator.AskAsync` already accepts `requirementSessionId` and `projectId` — it routes correctly without any changes.

The `KnowledgeRetrievalAgent.RetrieveAsync` already filters by either `requirementSessionId` or `projectId` via `IVectorStore.SearchAsync`.

`QdrantVectorStore` already:
- Upserts chunks with `RequirementSessionId` or `ProjectId` payload fields
- Searches with `RequirementSessionId` or `ProjectId` filter conditions
- `PromoteKnowledgeAsync` already sets `ProjectId` and removes `RequirementSessionId` from payload

**No AI layer files will be modified.**

---

## 8. Vector Store Changes

**No changes to `IVectorStore` or `QdrantVectorStore`.**

The `taskpilot_project_policies` collection name is already mapped in `GetCollectionName`.

Payload schema already includes:
- `DocumentId` ✓
- `RequirementSessionId` ✓
- `ProjectId` ✓
- `DocumentType` ✓
- `Category` ✓
- `SourceFile` ✓
- `ChunkIndex` ✓
- `Content` ✓
- `CreatedAt` ✓

Payload indexes already created in `EnsureCollectionsAsync` for all collections including `ProjectPolicies` ✓

**No vector store changes needed.**

---

## 9. Domain Model Change — Policy Entity

The `Policy` entity needs one new field to support linking a policy to a `RequirementSessionId` before a project exists:

```csharp
// In Policy.cs — add:
public Guid? RequirementSessionId { get; set; }
```

This enables duplicate detection by `RequirementSessionId` during Scenario 1 ingestion, and allows promotion to update the SQL record alongside the Qdrant metadata.

A new **EF Core migration** is required for this column.

---

## 10. Upload Workflow

### Scenario 1 — RequirementSessionId (pre-project)

```
Controller receives [ProjectId=null, RequirementSessionId=X, File]
  → IngestAsync(request)
    → Validate: RequirementSessionId or ProjectId must be provided
    → Validate: file present and non-empty
    → Resolve extractor by ContentType/FileName
    → Extract text
    → Validate: extracted text non-empty
    → Compute deterministic DocumentId = MD5($"{RequirementSessionId}_{extractedText}")
    → Acquire per-session semaphore
    → Check Policy table: DocumentId duplicate? → return early (idempotent)
    → Upload to Cloudinary (taskpilot/project-policies/sessions/{RequirementSessionId})
    → Categorize document
    → Chunk document
    → Set chunk fields: RequirementSessionId, Category, SourceFile, DocumentType="ProjectPolicy"
    → VectorStore.UpsertAsync(ProjectPolicies, chunks)
    → Save Policy { Scope=Project, RequirementSessionId=X, DocumentId, ... }
    → saveChangesAsync (called by Controller)
    → Return UploadProjectPolicyResponse
```

### Scenario 2 — ProjectId (post-project)

```
Controller receives [ProjectId=Y, RequirementSessionId=null, File]
  → IngestAsync(request)
    → Validate: ProjectId or RequirementSessionId must be provided
    → Validate project exists via IRepository<Project>
    → Validate: file present and non-empty
    → Resolve extractor
    → Extract text
    → Validate: extracted text non-empty
    → Compute deterministic DocumentId = MD5($"{ProjectId}_{extractedText}")
    → Acquire per-project semaphore
    → Check Policy table: DocumentId duplicate? → return early (idempotent)
    → Upload to Cloudinary (taskpilot/project-policies/projects/{ProjectId})
    → Categorize document
    → Chunk document
    → Set chunk fields: ProjectId, Category, SourceFile, DocumentType="ProjectPolicy"
    → VectorStore.UpsertAsync(ProjectPolicies, chunks)
    → Save Policy { Scope=Project, ProjectId=Y, DocumentId, ... }
    → saveChangesAsync (called by Controller)
    → Return UploadProjectPolicyResponse
```

**Duplicate prevention chain:**
1. SQL `Policy` table: check `DocumentId` match → skip entirely
2. Qdrant: `UpsertAsync` checks existing point IDs → skips embedding for already-stored chunks
3. Both gates together guarantee no duplicate vectors, chunks, or embeddings

---

## 11. Ask Workflow

```
POST /api/project-policies/ask
Body: { projectId OR requirementSessionId, question }

Controller → AskAsync(request)
  → Validate: projectId or requirementSessionId provided
  → If projectId provided: validate project exists
  → KnowledgeOrchestrator.AskAsync(
        collectionType: ProjectPolicies,
        requirementSessionId: request.RequirementSessionId,
        projectId: request.ProjectId,
        companyId: null,
        question: request.Question)
    → KnowledgeRetrievalAgent.RetrieveAsync(...)
      → VectorStore.SearchAsync with RequirementSessionId OR ProjectId filter
    → if 0 chunks → "Documents do not contain enough information"
    → KnowledgeAnswerAgent.GenerateAsync(question, chunks)
  → Map to ProjectPolicyAnswerResponse
    → Sources: chunk.SourceFile + collectionType.ToDisplayName()
  → Return Result.Success(response)
```

**Collection isolation:** Only `taskpilot_project_policies` is queried. Company retrieval is untouched.

---

## 12. Promote Workflow

```
POST /api/project-policies/promote
Body: { requirementSessionId, projectId }

Controller → PromoteAsync(request)  [no UnitOfWork.SaveChanges needed in controller for Qdrant-only step, but SQL update does need save]
  → Validate: requirementSessionId and projectId both provided
  → Validate project exists
  → Query Policy table: all policies with RequirementSessionId = X
  → If none found: return Result.Success() [idempotent — nothing to promote]
  → For each policy record:
      → If policy.ProjectId already == request.ProjectId: skip (idempotent)
  → Collect all DocumentIds from those policies
  → For each DocumentId: retrieve chunk IDs from Qdrant by RequirementSessionId filter
      → VectorStore.PromoteKnowledgeAsync(ProjectPolicies, projectId, chunkIds)
         → SetPayload: { ProjectId = projectId }
         → DeletePayload key: "RequirementSessionId"
         [No re-embedding. No re-chunking. Metadata only.]
  → Update SQL Policy records: set ProjectId = request.ProjectId, RequirementSessionId = null
  → saveChangesAsync (called by Controller)
  → Return Result.Success()
```

> [!IMPORTANT]
> Promotion is fully idempotent. If called twice with same parameters, Qdrant `SetPayload` is a no-op on already-promoted chunks, and SQL update is a no-op too.

---

## 13. Validation Flow

| Validation | Location | Error |
|---|---|---|
| Neither ProjectId nor RequirementSessionId provided | Service | `CommonErrors.InvalidInput("Either ProjectId or RequirementSessionId must be provided.")` |
| Both ProjectId and RequirementSessionId provided | Service | `CommonErrors.InvalidInput("Provide either ProjectId or RequirementSessionId, not both.")` |
| Project not found (when ProjectId given) | Service | `ProjectErrors.NotFound` |
| File is null or empty | Service | `CommonErrors.InvalidInput("File cannot be empty.")` |
| Unsupported file type | Service | `CommonErrors.InvalidInput($"Unsupported file type: {contentType}")` |
| Empty extracted text | Service | `CommonErrors.InvalidInput("Extracted text is empty.")` |
| Duplicate document (SQL) | Service | Early return success (idempotent, not an error) |
| Promote: RequirementSessionId missing | Service | `CommonErrors.InvalidInput(...)` |
| Promote: ProjectId missing | Service | `CommonErrors.InvalidInput(...)` |
| Promote: Project not found | Service | `ProjectErrors.NotFound` |
| Ask: neither id provided | Service | `CommonErrors.InvalidInput(...)` |
| Ask: Project not found (when ProjectId given) | Service | `ProjectErrors.NotFound` |

---

## 14. API Contracts

### POST `/api/project-policies/upload`
- Content-Type: `multipart/form-data`
- Body fields: `projectId` (Guid, optional), `requirementSessionId` (Guid, optional), `file` (IFormFile, required)
- Constraint: exactly one of `projectId` / `requirementSessionId` must be provided
- Success: `200 OK` — `UploadProjectPolicyResponse`
- Errors: `400 Bad Request` (validation), `404 Not Found` (project), `500` (storage failure)

### POST `/api/project-policies/ask`
```json
Request:
{
  "projectId": "guid (optional)",
  "requirementSessionId": "guid (optional)",
  "question": "string (min 3 chars, required)"
}

Response 200:
{
  "isSuccess": true,
  "data": {
    "answer": "...",
    "sources": [
      { "fileName": "...", "category": "Project Policy" }
    ]
  }
}
```

### POST `/api/project-policies/promote`
```json
Request:
{
  "requirementSessionId": "guid (required)",
  "projectId": "guid (required)"
}

Response 200:
{
  "isSuccess": true,
  "message": null
}
```

---

## 15. Sequence Diagrams

### Upload (Scenario 1 — RequirementSessionId)
```
Client → ProjectPoliciesController.Upload(form)
  → ProjectPolicyService.IngestAsync(request, saveChanges)
    → Validate identifier (RequirementSessionId present)
    → IDocumentTextExtractor.ExtractTextAsync(stream)
    → MD5(RequirementSessionId + text) → DocumentId
    → SemaphoreSlim.WaitAsync()
    → IRepository<Policy>.FindSingleAsync(p.DocumentId == documentId)
      [if found] → return Success (duplicate)
    → IFileStorageService.UploadFileAsync(file, path)
    → DocumentCategorizationAgent.CategorizeAsync(name, text)
    → ChunkingAgent.ChunkContentAsync(documentId, text)
    → Annotate chunks: RequirementSessionId, Category, SourceFile, DocumentType
    → IVectorStore.UpsertAsync(ProjectPolicies, chunks)
    → IRepository<Policy>.AddAsync(policy)
    → saveChanges()
    → SemaphoreSlim.Release()
  ← Result<UploadProjectPolicyResponse>
← 200 OK
```

### Ask (by ProjectId)
```
Client → ProjectPoliciesController.Ask(body)
  → ProjectPolicyService.AskAsync(request)
    → Validate: ProjectId present
    → IRepository<Project>.GetByIdAsync(projectId) → validate
    → KnowledgeOrchestrator.AskAsync(ProjectPolicies, null, projectId, null, question)
      → KnowledgeRetrievalAgent.RetrieveAsync(...)
        → IVectorStore.SearchAsync(ProjectPolicies, projectId filter)
      → [0 chunks] → "Documents do not contain enough information"
      → KnowledgeAnswerAgent.GenerateAsync(question, chunks)
    → Map sources with ToDisplayName()
  ← Result<ProjectPolicyAnswerResponse>
← 200 OK
```

### Promote
```
Client → ProjectPoliciesController.Promote(body)
  → ProjectPolicyService.PromoteAsync(request)
    → Validate both ids present
    → IRepository<Project>.GetByIdAsync(projectId) → validate
    → IRepository<Policy>.FindAsync(p.RequirementSessionId == sessionId)
    → [none found] → return Success (idempotent)
    → foreach policy document:
        → IVectorStore.PromoteKnowledgeAsync(ProjectPolicies, projectId, chunkIds)
           → Qdrant SetPayload(ProjectId) + DeletePayload(RequirementSessionId)
        → policy.ProjectId = projectId; policy.RequirementSessionId = null
    → IRepository<Policy>.UpdateRange(policies)
    → _unitOfWork.SaveChangesAsync()  [called inside service for promote]
  ← Result.Success()
← 200 OK
```

> [!NOTE]
> For Promote, `SaveChangesAsync` is called inside the service directly (not passed as a delegate) since the controller does not need its own UoW call — the service owns the full atomic operation. This is consistent with how other multi-step services handle saves internally.

---

## 16. Dependency Injection Updates

**File:** `TaskPilot.Services/DependencyInjection.cs`

Add one line:
```csharp
services.AddScoped<IProjectPolicyService, ProjectPolicyService>();
```

No AI layer DI changes needed — all agents and orchestrators are already registered.

---

## 17. New Error Additions — `KnowledgeErrors.cs`

```csharp
public static class KnowledgeErrors
{
    // existing:
    public static readonly Error MissingTenantIsolation = ...;

    // new:
    public static readonly Error AmbiguousTenantIdentifier =
        new("AMBIGUOUS_TENANT_IDENTIFIER", ErrorType.Validation,
            "Provide either ProjectId or RequirementSessionId, not both.");

    public static readonly Error MissingProjectPolicyIdentifier =
        new("MISSING_PROJECT_POLICY_IDENTIFIER", ErrorType.Validation,
            "Either ProjectId or RequirementSessionId must be provided.");
}
```

---

## 18. Migration

One EF Core migration:

```
Add-Migration AddPolicyRequirementSessionId
```

Adds nullable column `RequirementSessionId (uniqueidentifier NULL)` to `Policies` table.

---

## 19. Test Plan

All tests in `TaskPilot.Tests`. Use `xUnit` + `Moq` (matching existing test style).

### Unit Tests — `ProjectPolicyServiceTests.cs`

| # | Scenario | Verifies |
|---|---|---|
| T01 | Upload with ProjectId — first time | Policy saved, vectors upserted, correct chunk metadata |
| T02 | Upload with RequirementSessionId — first time | Policy saved, vectors upserted, RequirementSessionId in chunks |
| T03 | Upload with ProjectId — duplicate document | Returns success with ChunksCreated=0, no double upsert |
| T04 | Upload with RequirementSessionId — duplicate | Same as T03 for session scope |
| T05 | Upload — no identifier provided | Returns `MissingProjectPolicyIdentifier` error (400) |
| T06 | Upload — both identifiers provided | Returns `AmbiguousTenantIdentifier` error (400) |
| T07 | Upload — unsupported file type | Returns `InvalidInput` error |
| T08 | Upload — empty extracted text | Returns `InvalidInput` error |
| T09 | Upload — project not found | Returns `ProjectErrors.NotFound` |
| T10 | Ask with ProjectId — chunks found | Returns answer + sources with "Project Policy" category |
| T11 | Ask with RequirementSessionId | Passes RequirementSessionId to orchestrator, not ProjectId |
| T12 | Ask — no chunks retrieved | Returns "not enough information" message |
| T13 | Ask — no identifier | Returns `MissingProjectPolicyIdentifier` |
| T14 | Ask — project not found | Returns `ProjectErrors.NotFound` |
| T15 | Promote — success path | Qdrant metadata updated, SQL records updated |
| T16 | Promote — idempotent (called twice) | Second call is no-op, returns Success |
| T17 | Promote — no policies found for session | Returns Success (nothing to do) |
| T18 | Promote — project not found | Returns `ProjectErrors.NotFound` |
| T19 | Promote — vectors NOT re-embedded | `IEmbeddingService.GenerateEmbeddingAsync` never called |
| T20 | Promote — chunks NOT re-chunked | `ChunkingAgent.ChunkContentAsync` never called |
| T21 | Collection isolation | `SearchAsync` always called with `ProjectPolicies`, never `CompanyPolicies` |
| T22 | Tenant isolation (Qdrant search) | Search filter contains only ProjectId OR RequirementSessionId, not both |
| T23 | Hallucination prevention | When 0 chunks, no `KnowledgeAnswerAgent.GenerateAsync` call made |
| T24 | Metadata update after promote | Policy.ProjectId set, Policy.RequirementSessionId null |
| T25 | Duplicate chunk prevention | `UpsertAsync` skips existing point IDs (verified via mock call count) |

---

## 20. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| RequirementSessionId may not correspond to any known entity | Medium | No repository for sessions — validated only by presence of uploaded policies. Document this behavior. |
| Partial promote failure (Qdrant succeeds, SQL fails) | Medium | Promote is idempotent — re-running recovers. Consider wrapping in try/catch with structured logging. |
| `PromoteKnowledgeAsync` chunk ID discovery requires Qdrant scroll/retrieve | Low | Use Qdrant scroll by filter (RequirementSessionId) to get all chunk IDs before SetPayload. Existing `QdrantVectorStore` may need a `GetChunkIdsByFilterAsync` helper if not already present. |
| Large document uploads exhausting Cloudinary or memory | Low | Use `IFormFile.OpenReadStream()` streaming (already done in Company policy). |
| Semaphore leak on exception | Low | `try/finally` pattern already established in `CompanyPolicyService` — replicate exactly. |

---

## 21. Final Implementation Order

```
Step 1 — Domain
  ├── Add RequirementSessionId to Policy entity
  └── Add new errors to KnowledgeErrors.cs

Step 2 — Migration
  └── Add-Migration AddPolicyRequirementSessionId + Update-Database

Step 3 — DTOs
  └── Create TaskPilot.DTOs/AI/ProjectPolicies/ folder + all 6 DTO files

Step 4 — Service Interface
  └── Create IProjectPolicyService.cs

Step 5 — Service Implementation
  └── Create ProjectPolicyService.cs
      ├── IngestAsync (both scenarios)
      ├── AskAsync
      └── PromoteAsync

Step 6 — DI Registration
  └── Add AddScoped<IProjectPolicyService, ProjectPolicyService>

Step 7 — Controller
  └── Create ProjectPoliciesController.cs
      ├── POST /upload
      ├── POST /ask
      └── POST /promote

Step 8 — Tests
  └── Create ProjectPolicyServiceTests.cs (T01–T25)

Step 9 — Build + Verify
  └── dotnet build (zero warnings target)
  └── dotnet test
```

---

## Summary of New Files

| File | Project |
|---|---|
| `TaskPilot.DTOs/AI/ProjectPolicies/UploadProjectPolicyRequest.cs` | DTOs |
| `TaskPilot.DTOs/AI/ProjectPolicies/IngestProjectPolicyRequest.cs` | DTOs |
| `TaskPilot.DTOs/AI/ProjectPolicies/UploadProjectPolicyResponse.cs` | DTOs |
| `TaskPilot.DTOs/AI/ProjectPolicies/ProjectPolicyQuestionRequest.cs` | DTOs |
| `TaskPilot.DTOs/AI/ProjectPolicies/ProjectPolicyAnswerResponse.cs` | DTOs |
| `TaskPilot.DTOs/AI/ProjectPolicies/PromoteProjectPolicyRequest.cs` | DTOs |
| `TaskPilot.Services/Interfaces/IProjectPolicyService.cs` | Services |
| `TaskPilot.Services/ProjectPolicyService.cs` | Services |
| `TaskPilot.Presentation/Controllers/ProjectPoliciesController.cs` | Presentation |
| `TaskPilot.Tests/ProjectPolicyServiceTests.cs` | Tests |

## Summary of Modified Files

| File | Change |
|---|---|
| `TaskPilot.Models/Entities/Policy.cs` | Add `RequirementSessionId` property |
| `TaskPilot.Models/Common/Errors/KnowledgeErrors.cs` | Add 2 new error constants |
| `TaskPilot.Services/DependencyInjection.cs` | Register `IProjectPolicyService` |
| EF Core Migrations | New migration for `RequirementSessionId` column |
