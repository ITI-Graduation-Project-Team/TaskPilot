# Result Pattern Guide & Compliance for TaskPilot

This document outlines the standard architecture and implementation rules for using the **Result Pattern** across the `TaskPilot` codebase. All coding agents and developers must strictly adhere to these practices to ensure clean code, unified HTTP error handling, and robust exception-free flow control.

---

## 1. Overview & Architectural Goals
In TaskPilot, we avoid using exceptions (`throw`) for anticipated domain or business rule failures (e.g., resource not found, invalid validation constraints, conflicts). Exceptions should only represent unexpected system failures (database down, file write errors, etc.).

Instead, all application services return a **Result** envelope that explicitly states whether the operation succeeded or failed. This allows the API layer to map failures to HTTP status codes uniformly and securely.

---

## 2. Core Result Types

The following types are defined under the namespace `TaskPilot.Models.Common.Results` and `TaskPilot.Models.Common.Errors`:

### `Result` (No Data Return)
Represents operations that do not return data (e.g., Update, Delete, Confirm).
* **Success**: `Result.Success()`
* **Failure**: `Result.Failure(Error error)`

### `Result<T>` (With Data Return)
Represents operations that return data on success (e.g., Get, Create, Suggest).
* **Success**: `Result.Success(T value)`
* **Failure**: `Result.Failure<T>(Error error)`

### `Error` & `ErrorType`
An `Error` consists of:
1. `Code`: A unique machine-readable string (e.g., `PROJECT_NOT_FOUND`).
2. `Type`: An enum value from `ErrorType` denoting the HTTP classification:
   * `ErrorType.Validation` $\rightarrow$ Maps to `400 BadRequest`
   * `ErrorType.NotFound` $\rightarrow$ Maps to `404 NotFound`
   * `ErrorType.Conflict` $\rightarrow$ Maps to `409 Conflict`
   * `ErrorType.Unauthorized` $\rightarrow$ Maps to `401 Unauthorized`
   * `ErrorType.Forbidden` $\rightarrow$ Maps to `403 Forbidden`
   * `ErrorType.Failure` $\rightarrow$ Maps to `500 InternalServerError`
3. `Description`: Optional default human-readable description.

---

## 3. Step-by-Step Implementation Guide

### Step 1: Define Custom Errors
Errors are defined inside `TaskPilot.Models/Common/Errors/`. Create a static class named `[Feature]Errors` if it does not already exist.

```csharp
using TaskPilot.Models.Common.Errors;

namespace TaskPilot.Models.Common.Errors
{
    public static class SprintErrors
    {
        public static readonly Error NoUserStoriesSelected = 
            new("NO_USER_STORIES_SELECTED", ErrorType.Validation, "At least one UserStory must be selected for the sprint.");
    }
}
```

### Step 2: Use in Service Interfaces
All service methods must return `Task<Result>` or `Task<Result<T>>`.

```csharp
using System.Threading.Tasks;
using TaskPilot.Models.Common.Results;
using TaskPilot.DTOs.Sprints;

namespace TaskPilot.Services.Interfaces
{
    public interface ISprintConfirmationService
    {
        Task<Result<ConfirmSprintResult>> ConfirmAsync(
            Guid projectId,
            ConfirmSprintRequest request,
            CancellationToken cancellationToken = default);
    }
}
```

### Step 3: Implement the Service
Check validations and business constraints early. Return failures instead of throwing exceptions.

```csharp
using System.Threading.Tasks;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;

public async Task<Result<ConfirmSprintResult>> ConfirmAsync(
    Guid projectId,
    ConfirmSprintRequest request,
    CancellationToken cancellationToken = default)
{
    // 1. Validation
    if (request.UserStoryIds == null || !request.UserStoryIds.Any())
    {
        return Result.Failure<ConfirmSprintResult>(SprintErrors.NoUserStoriesSelected);
    }

    var project = await _projectRepository.GetByIdAsync(projectId);
    if (project is null)
    {
        return Result.Failure<ConfirmSprintResult>(CommonErrors.NotFound("Project"));
    }

    try
    {
        // 2. Business logic & Transaction
        var sprint = new Sprint { ... };
        await _sprintRepository.AddAsync(sprint);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var data = new ConfirmSprintResult { SprintId = sprint.Id };
        return Result.Success(data);
    }
    catch (Exception ex)
    {
        return Result.Failure<ConfirmSprintResult>(CommonErrors.ServerError(ex.Message));
    }
}
```

### Step 4: Map in Controller Endpoints
1. Inherit the controller class from **`ApiControllerBase`** (instead of `ControllerBase`).
2. Action methods **must return `Task<ActionResult>` or `Task<ActionResult<T>>`** (never return the raw `IActionResult`).
3. Call `return HandleResult(result)` to automatically delegate translation and formatting to the centralized mapper.

```csharp
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TaskPilot.DTOs.Sprints;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/sprints")]
    public class SprintsController : ApiControllerBase
    {
        private readonly ISprintConfirmationService _sprintConfirmationService;

        public SprintsController(ISprintConfirmationService sprintConfirmationService)
        {
            _sprintConfirmationService = sprintConfirmationService;
        }

        [HttpPost("confirm")]
        public async Task<ActionResult> Confirm(
            Guid projectId,
            [FromBody] ConfirmSprintRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _sprintConfirmationService
                .ConfirmAsync(projectId, request, cancellationToken);

            return HandleResult(result);
        }
    }
}
```

---

## 4. Best Practices Checklist
- [ ] **No exceptions for business validations**: Never throw `ArgumentException`, `InvalidOperationException`, or custom exception classes for validation checks inside services. Return a `Result.Failure(...)` with the appropriate `ErrorType`.
- [ ] **Centralized translation**: Do not manually handle `Ok(...)`, `BadRequest(...)`, `NotFound(...)` status codes in controllers. Let `HandleResult(result)` do the mapping.
- [ ] **Proper generic parameters**: Use `Result.Failure<T>(error)` when inside a method returning a typed data wrapper.
- [ ] **Clean signatures**: Ensure controller methods return typed `ActionResult` rather than the untyped `IActionResult` interface to support Swagger schema generation.
