# Frontend Task: Handle Active Sprint Validation for Sprint Planning

## Context & Requirement
The backend now enforces a validation rule that prevents Project Managers (PMs) from generating AI sprint suggestions or planning a new sprint while there is already an **active sprint** for the project.

When an API call to generate a sprint suggestion is triggered on a project that has an active sprint, the backend returns a `409 Conflict` (or Result failure) with the error code:
```json
{
  "code": "ANOTHER_SPRINT_ALREADY_ACTIVE",
  "message": "Sprint.AnotherAlreadyActive",
  "type": "Conflict"
}
```

---

## Objectives for Frontend Agent

1. **Pre-validation & UI State Handling**:
   - Check if the current project already has an active sprint (`sprints.some(s => s.status === 'Active')`).
   - If an active sprint exists:
     - Show an informational warning banner on the Sprint Planning page:
       > ℹ️ **Active Sprint in Progress**: An active sprint is currently running for this project. You cannot plan or generate suggestions for a new sprint until the current active sprint is completed or closed.
     - Disable the **"Generate AI Sprint Suggestion"** button with a clear tooltip: *"Complete the current active sprint before planning a new one."*

2. **API Error Handling**:
   - Intercept error responses from `POST /api/projects/{projectId}/sprint-suggestions` (or equivalent endpoint).
   - If `error.code === "ANOTHER_SPRINT_ALREADY_ACTIVE"`:
     - Display an error modal / toast notification informing the PM that an active sprint is already running.
     - Include a direct call-to-action (CTA) button: **"View Active Sprint"** that navigates the user to the active sprint board (`/projects/{projectId}/sprints/active`).

3. **User Flow & UX Polish**:
   - Ensure the UI dynamically updates when an active sprint is completed so that the sprint planning feature unlocks automatically.
   - Support localized strings for English (`en`) and Arabic (`ar`) for error messages, warnings, and CTA buttons.

---

## Expected API Response Example
```json
{
  "isSuccess": false,
  "error": {
    "code": "ANOTHER_SPRINT_ALREADY_ACTIVE",
    "type": "Conflict",
    "description": "Sprint.AnotherAlreadyActive"
  }
}
```

---

## Action Plan Checklist for Frontend Implementation
- [ ] Check `activeSprint` status in `useProjectSprints` / Sprint store.
- [ ] Add conditional guard on Sprint Planning view to display active sprint banner and disable generation actions.
- [ ] Handle `ANOTHER_SPRINT_ALREADY_ACTIVE` error code in API service / toast notifier with a link to the active sprint.
- [ ] Add localized string keys (`en` & `ar`).
- [ ] Verify flow: Project with Active Sprint -> Button disabled & banner shown -> Complete Sprint -> Sprint planning unlocks.
