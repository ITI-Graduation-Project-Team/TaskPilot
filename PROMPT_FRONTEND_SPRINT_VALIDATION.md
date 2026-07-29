# Frontend Task: Handle Sprint Planning Validation for Empty Project Teams

## Context & Requirement
The backend now enforces a validation rule preventing Project Managers (PMs) from generating AI sprint suggestions, confirming sprint creation, or starting sprints if a project has **0 assigned employees**.

When an API call for sprint planning is triggered on a project without team members, the backend returns a `400 Bad Request` or validation failure with the error code:
```json
{
  "code": "NO_EMPLOYEES_ASSIGNED",
  "message": "Cannot perform sprint planning for a project with no assigned employees.",
  "type": "Validation"
}
```

---

## Objectives for Frontend Agent

1. **Pre-validation & UI State Handling**:
   - Check the project's employee count / team member list before enabling sprint planning actions.
   - If the project team is empty (`project.employees.length === 0`), show a warning banner on the Sprint Planning view:
     > ⚠️ **No Team Members Assigned**: You cannot create or plan sprints until at least one employee is assigned to this project.
   - Disable or visually indicate the action buttons for **"Generate AI Sprint Suggestion"**, **"Confirm Sprint"**, and **"Start Sprint"** when no employees are assigned, with a helpful tooltip.

2. **API Error Handling**:
   - Intercept error responses from `POST /api/projects/{projectId}/sprint-suggestions`, `POST /api/projects/{projectId}/sprints`, and `POST /api/projects/{projectId}/sprints/{sprintId}/start`.
   - If `error.code === "NO_EMPLOYEES_ASSIGNED"`:
     - Show an error modal / toast notification explaining that employees must be assigned to the project first.
     - Include a direct call-to-action (CTA) button: **"Assign Employees"** or **"Invite Team"** that navigates the PM directly to the Project Team Management page (`/projects/{projectId}/team`).

3. **User Flow & UX Polish**:
   - Ensure local state updates immediately when team members are added so the sprint planning feature unlocks dynamically without requiring a full page refresh.
   - Support both English and Arabic translations for warning banners, error messages, and CTA buttons.

---

## Expected API Response Example
```json
{
  "isSuccess": false,
  "error": {
    "code": "NO_EMPLOYEES_ASSIGNED",
    "type": "Validation",
    "description": "Cannot perform sprint planning for a project with no assigned employees."
  }
}
```

---

## Action Plan Checklist for Frontend Implementation
- [ ] Update `useProjectTeam` / Project details store to track `employeeCount`.
- [ ] Add conditional guard in `SprintPlanningContainer` / `SprintBoard` to show empty team banner.
- [ ] Update `SprintPlanningService` / API client error handler for `NO_EMPLOYEES_ASSIGNED`.
- [ ] Add localized string keys for English (`en`) and Arabic (`ar`).
- [ ] Test flow: Navigate to project with 0 team members -> Verify UI disabled & banner visible -> Add employee -> Verify sprint planning unlocks.
