# ARCHITECTURE BASELINE

This document serves as the binding reference for all future development on the TaskPilot codebase. It establishes the "ground truth" of how the application is currently architected, structured, and styled. Every future change must align with these established patterns unless explicitly instructed otherwise.

## ═══════════════════════════════════
## PART 1 — BACKEND ANALYSIS (.NET Core)
## ═══════════════════════════════════

### 1. Solution Structure
The backend is a multi-project .NET solution (`TaskPilot.sln`) organized primarily by technical concern, heavily leaning towards a Layered/N-Tier Architecture.
- **TaskPilot.Presentation**: The API entry point. Contains Controllers, Middlewares, Hubs, and standard ASP.NET Core startup wiring (Program.cs).
- **TaskPilot.Services**: Contains core business logic and background jobs. Interacts with repositories and external systems.
- **TaskPilot.Data**: The Data Access layer. Contains the EF Core `ApplicationDbContext`, Identity configuration, and Repositories.
- **TaskPilot.AI**: Encapsulates AI-specific logic using SemanticKernel, Qdrant, and prompt YAML files (RAG pipeline, requirements analysis).
- **TaskPilot.Infrastructure**: Contains integrations with external services (Google Calendar, MailKit, Cloudinary, PayPal).
- **TaskPilot.Models**: Core domain entities and enumerations.
- **TaskPilot.DTOs**: Data Transfer Objects used to shape requests and responses across layers.
- **TaskPilot.Tests**: xUnit test project.

### 2. Architectural Style
The backend implements a **Layered N-Tier Architecture**. 
Evidence: 
- Controllers in Presentation layer handle HTTP routing and delegate to Services via interfaces (e.g., `IProjectService`).
- Services in the Services layer hold business logic and rely on generic and specific Repositories in the Data layer.
- The `Result<T>` pattern is utilized in Services to return status without throwing exceptions, which the Controllers map to HTTP responses.

### 3. Framework & Nuget Packages
- **Framework**: .NET 10.0 (`net10.0`)
- **Key Packages**:
  - ORM: `Microsoft.EntityFrameworkCore` (10.0.7), `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
  - Background Jobs: `Hangfire.AspNetCore` (1.8.23)
  - AI/Vector Search: `Microsoft.SemanticKernel` (1.76.0), `Qdrant.Client` (1.18.1)
  - Testing: `xunit` (2.9.3), `Moq` (4.20.72), `MockQueryable.Moq`

### 4. Data Access
- **ORM**: Entity Framework Core targeting SQL Server.
- **Pattern**: A standard **Repository Pattern** is used (`IRepository<T>`, `IUserStoryRepository`). 
- **Unit of Work**: A Unit of Work pattern (`IUnitOfWork`) is explicitly used. Services do *not* call `SaveChanges`. They manipulate data via Repositories, and the Controller dictates when to call `_unitOfWork.SaveChangesAsync()`.

### 5. API Layer
- **Controllers**: Derive from an abstract `ApiControllerBase`.
- **Response Wrapping**: All responses are wrapped using `ApiResponse.Success` or `ApiResponse.Fail` within `ApiControllerBase.HandleResult<T>()`. This provides a perfectly consistent JSON envelope for the frontend.
- **Error Mapping**: `ApiControllerBase` acts as an anti-corruption layer, translating domain `Result.ErrorType` (Validation, NotFound, Conflict) into standard HTTP status codes (400, 404, 409).
- **Routing**: Standard `[Route("api/[controller]")]`.

### 6. Dependency Injection
- Extension methods (e.g., `AddData()`, `AddServices()`) are used to neatly bundle DI registrations per project.
- Repositories and Services are predominantly registered as `Scoped`. 

### 7. Cross-Cutting Concerns
- **Logging**: Standard ASP.NET Core `ILogger<T>`.
- **Background Jobs**: Hangfire is configured using SQL Server storage. Jobs are registered in `Program.cs` (e.g., `SprintRiskDetectionJob`).
- **Real-time**: SignalR is used via a `NotificationHub`.

### 8. Authentication & Authorization
- **Authentication**: JWT Bearer Tokens. Configuration is standard (Issuer, Audience, Signing Key).
- **Authorization**: Attribute-based (`[Authorize(Roles = "ProjectManager")]`) combined with Policy-based logic (e.g., `"ProfileComplete"` policy checking for claims/roles).

### 9. Configuration
- Relies on standard `appsettings.json` and environment variables. Connection strings are pulled from `GetConnectionString("DefaultConnection")`. 

### 10. Testing
- Contains a test project using `xUnit` and `Moq`. Uses `MockQueryable.Moq` for mocking Entity Framework `IQueryable` datasets.


## ═══════════════════════════════════
## PART 2 — FRONTEND ANALYSIS (Angular)
## ═══════════════════════════════════

### 1. Framework
- **Version**: Angular ^21.2.0
- **Approach**: **Standalone Components** heavily utilized. There are no traditional NgModules (`app.module.ts`); instead, `app.config.ts` wires the application.

### 2. Project Structure
- The structure mimics **Feature-Sliced Design (FSD)** principles.
- Folders: `/entities`, `/features`, `/pages`, `/shared`, `/widgets`.
- Features encapsulate their own UI and specific logic, while shared holds global interceptors, guards, and standard UI blocks.

### 3. State Management
- **Pattern**: **Angular Signals** (`signal`, `computed`) encapsulated in injectable services (e.g., `ProjectStateService`). 
- No heavy state management libraries like NgRx. State relies purely on Signals and is occasionally persisted to `localStorage`.

### 4. Routing
- Uses `provideRouter` in `app.config.ts`.
- Implements `withViewTransitions` and `withInMemoryScrolling`.
- Route guards (`authGuard`, `roleGuard`, `projectSetupGuard`) are implemented as functional `CanActivateFn` guards.

### 5. API Communication
- **Dual approach**: The app configures Angular's native `provideHttpClient` with functional interceptors (`authInterceptor`, `loadingInterceptor`, `languageInterceptor`).
- However, heavy API communication is performed via an explicitly configured **Axios instance** (`apiClient` in `axios.instance.ts`).
- Axios interceptors are used to automatically attach Bearer tokens, show/hide global loading indicators, and handle token refresh logic silently on 401s.

### 6. UI & Theming
- **CSS Framework**: **Tailwind CSS**.
- **Theming**: Custom CSS variables defined at the root (e.g., `app.html` `<style>` blocks defining OKLCH colors, gradients, and fonts like Inter).
- No external heavy component libraries (like Angular Material) are apparent in dependencies. The app favors native HTML styled with Tailwind utility classes.

### 7. Forms
- **Reactive Forms** (`ReactiveFormsModule`, `FormBuilder`, `FormGroup`) are used for complex inputs (e.g., `company-setup` component).

### 8. Authentication
- Tokens are stored in cookies (managed via a `cookie.helper.ts`).
- Axios interceptors seamlessly parse these cookies, inject the Authorization header, and orchestrate the refresh-token cycle automatically when tokens expire.

### 9. Build & Tooling
- Angular CLI build process using `@angular/build:application`.
- Testing framework is configured as **Vitest** (modern, fast alternative to Karma/Jasmine).

## ═══════════════════════════════════
## PART 3 — CROSS-CUTTING / END-TO-END
## ═══════════════════════════════════

### 1. Business Purpose
TaskPilot is an AI-augmented Agile Project Management application. Primary flows include Company/Project initialization, Sprint planning, User Story / Task management, and AI-driven Requirement generation (WBS extraction, project policy parsing via RAG pipelines).

### 2. E2E Request Lifecycle Example: Creating a Project
1. **Frontend**: User submits form. `ProjectStateService.createNewProject()` is called.
2. **Frontend HTTP**: `apiClient.post('/Projects', payload)` fires via Axios. Interceptor attaches JWT token from cookies and triggers `LoadingService`.
3. **Backend Controller**: `ProjectsController.Create([FromBody] CreateProjectDto)` receives the payload.
4. **Backend Service**: Controller calls `_projectService.CreateAsync(dto)`.
5. **Backend Logic/Data**: `ProjectService` validates manager/company existence, maps to `Project` entity, and calls `_projectRepo.AddAsync(project)`. It returns a domain `Result<ProjectDto>`.
6. **Backend Save**: Controller checks if `result.IsSuccess` and calls `_unitOfWork.SaveChangesAsync()`.
7. **Backend Response**: Controller calls `HandleCreated(result)`, mapping the domain result into a standard 201 HTTP JSON `ApiResponse`.
8. **Frontend State**: `createNewProject` receives the response, calls `this.loadProjects()` to refresh the Signals store, and loading hides.

### 3. Syncing Contracts
- DTOs and models appear manually mirrored between the backend (`TaskPilot.DTOs`) and frontend (`src/app/shared/models`). There is no obvious automated OpenAPI/Swagger code generation pipeline active in the repository.

### 4. Inconsistencies / Deviations
- **HTTP Clients**: There is a mix of `provideHttpClient` interceptors (Angular native) and an explicit Axios instance (`apiClient`). This indicates either a migration in progress or split responsibilities.
- **State Logic**: Some local storage usage is scattered inside Signal services rather than abstracted into a pure persistence layer.

## ═══════════════════════════════════
## PART 4 — SECURITY BASELINE (OWASP)
## ═══════════════════════════════════
*Observational snapshot. No remediation required immediately.*

- **Broken Access Control**: Strong. Strict `[Authorize(Roles="")]` combined with policy assertions on the backend. Frontend route guards prevent unauthorized navigation.
- **Injection Risks**: Strong. EF Core entirely mitigates SQL injection.
- **Authentication/Session**: Solid implementation of JWT with Refresh Tokens. Storing tokens in cookies reduces XSS risk compared to localStorage, provided they are marked `HttpOnly` (to be verified).
- **Sensitive Data Exposure**: `appsettings.json` is used. Attention is needed to ensure secrets (JWT keys, Connection Strings) are injected via environment variables or Key Vault in production.
- **CORS Configuration**: Explicit policy (`AllowFrontend`) is bound to known origins (`localhost:4200`, `taskpilotapi.runasp.net`), which is secure.
- **Security Headers**: No explicit `UseHsts` or CSP headers were visibly registered in the middleware pipeline; IIS/Kestrel defaults apply.
- **Input Validation**: Backend correctly returns HTTP 400 for validation errors mapped via the `Result` pattern. Frontend utilizes Angular Reactive Forms validation.
- **Dependency Vulnerabilities**: Built on very recent stacks (.NET 10, Angular 21). Risk is inherently low right now.
- **Rate Limiting**: No explicit rate limiting middleware is wired into `Program.cs`.

---
**END OF BASELINE**
