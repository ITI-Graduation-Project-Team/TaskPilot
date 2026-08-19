using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common.Results;
using TaskPilot.DTOs.Sprint;
using TaskPilot.Data.Context;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;

namespace TaskPilot.Presentation.Controllers
{
    [Route("api/sprints")]
    [ApiController]
    [Authorize]
    public class SprintRiskController : ControllerBase
    {
        private readonly ISprintRiskService _riskService;
        private readonly ICurrentUserService _currentUserService;

        public SprintRiskController(ISprintRiskService riskService, ICurrentUserService currentUserService)
        {
            _riskService = riskService;
            _currentUserService = currentUserService;
        }

        [HttpGet("{sprintId}/risks")]
        [ProducesResponseType(typeof(Result<List<SprintRiskAlertDto>>), 200)]
        public async Task<IActionResult> GetRisks(Guid sprintId)
        {
            var result = await _riskService.GetAlertsAsync(sprintId);
            return Ok(result);
        }

        [HttpPatch("{sprintId}/risks/{alertId}/dismiss")]
        [ProducesResponseType(typeof(Result), 200)]
        [ProducesResponseType(typeof(Result), 400)]
        public async Task<IActionResult> DismissRisk(Guid sprintId, Guid alertId)
        {
            var result = await _riskService.DismissAlertAsync(alertId, _currentUserService.UserId.Value);
            if (!result.IsSuccess)
                return BadRequest(result);
                
            return Ok(result);
        }

        [HttpGet("{sprintId}/risks/{alertId}/simulate")]
        [ProducesResponseType(typeof(Result<SprintRiskSimulationResponseDto>), 200)]
        [ProducesResponseType(typeof(Result<SprintRiskSimulationResponseDto>), 400)]
        public async Task<IActionResult> SimulateResolution(Guid sprintId, Guid alertId, CancellationToken ct)
        {
            var result = await _riskService.SimulateAsync(alertId, ct);
            if (!result.IsSuccess)
                return BadRequest(result);

            return Ok(result);
        }
        [HttpGet("{sprintId}/team-pulse")]
        [ProducesResponseType(typeof(Result<TeamPulseDto>), 200)]
        [ProducesResponseType(typeof(Result<TeamPulseDto>), 400)]
        public async Task<IActionResult> GetTeamPulse(Guid sprintId, CancellationToken ct)
        {
            var result = await _riskService.GetTeamPulseAsync(sprintId, ct);
            if (!result.IsSuccess)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("{sprintId}/team-pulse/trigger-analysis")]
        [ProducesResponseType(typeof(Result), 200)]
        public async Task<IActionResult> TriggerAnalysis(Guid sprintId, CancellationToken ct)
        {
            await _riskService.AnalyzeSprintBurnoutAsync(sprintId, ct);
            return Ok(Result.Success());
        }

        [HttpGet("{sprintId}/audit-log")]
        [ProducesResponseType(typeof(Result<List<ActivityFeedItemDto>>), 200)]
        [ProducesResponseType(typeof(Result<List<ActivityFeedItemDto>>), 400)]
        public async Task<IActionResult> GetFullAuditLog(Guid sprintId, CancellationToken ct)
        {
            var result = await _riskService.GetFullAuditLogAsync(sprintId, ct);
            if (!result.IsSuccess)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("{sprintId}/recent-activity")]
        [ProducesResponseType(typeof(Result<List<ActivityFeedItemDto>>), 200)]
        [ProducesResponseType(typeof(Result<List<ActivityFeedItemDto>>), 400)]
        public async Task<IActionResult> GetRecentActivity(Guid sprintId, CancellationToken ct)
        {
            var result = await _riskService.GetRecentActivityAsync(sprintId, ct);
            if (!result.IsSuccess)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("seed-mock-data")]
        [AllowAnonymous]
        public async Task<IActionResult> SeedMockData([FromServices] ApplicationDbContext db, [FromServices] Microsoft.AspNetCore.Identity.UserManager<User> userManager, [FromServices] Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole<Guid>> roleManager)
        {
            // Ensure roles exist in AspNetRoles
            string pmRole = UserRole.ProjectManager.ToString();
            string empRole = UserRole.Employee.ToString();
            
            if (!await roleManager.RoleExistsAsync(pmRole)) 
            {
                var pmrResult = await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole<Guid> { Name = pmRole, NormalizedName = pmRole.ToUpper() });
                if (!pmrResult.Succeeded) throw new Exception("Failed to create PM role: " + string.Join(", ", pmrResult.Errors.Select(e => e.Description)));
            }
            if (!await roleManager.RoleExistsAsync(empRole)) 
            {
                var emrResult = await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole<Guid> { Name = empRole, NormalizedName = empRole.ToUpper() });
                if (!emrResult.Succeeded) throw new Exception("Failed to create EMP role: " + string.Join(", ", emrResult.Errors.Select(e => e.Description)));
            }

            var randomSuffix = Guid.NewGuid().ToString().Substring(0, 4);

            // 1. Create Project Manager (first, without company, to avoid circular FK)
            var pmEmail = $"pm_ai_{randomSuffix}@mock.taskpilot.com";
            var pm = new ProjectManager 
            { 
                UserName = pmEmail, Email = pmEmail, EmailConfirmed = true,
                FirstNameEn = "Master", LastNameEn = "Manager"
            };
            var createPmResult = await userManager.CreateAsync(pm, "Password123!");
            if (!createPmResult.Succeeded) throw new Exception("Failed to create PM: " + string.Join(", ", createPmResult.Errors.Select(e => e.Description)));
            
            var addRoleResult = await userManager.AddToRoleAsync(pm, pmRole);
            if (!addRoleResult.Succeeded) throw new Exception("Failed to add role to PM: " + string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));

            // 2. Create Company (assign PM as owner)
            var company = new Company { Name = "AI Demo Corp", OwnerId = pm.Id };
            db.Set<Company>().Add(company);
            await db.SaveChangesAsync();

            // 3. Link PM to Company
            pm.CompanyId = company.Id;
            db.Set<ProjectManager>().Update(pm);

            // 4. Create Project & Sprint
            var project = new Project
            {
                NameEn = "NextGen AI Platform", DescriptionEn = "Pure data simulation project for AI Burnout Analysis.",
                Manager = pm, Company = company, Status = ProjectStatus.Active
            };
            db.Set<Project>().Add(project);

            var sprint = new Sprint
            {
                Project = project, TitleEn = "Sprint 1: The Ultimate Test",
                StartDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddHours(2),
                Status = SprintStatus.Active
            };
            db.Set<Sprint>().Add(sprint);

            // 5. Create or Fetch Existing Skills
            var skillNames = new[] { "Angular", "React", ".NET Core", "SQL Server", "Figma", "Docker", "Kubernetes" };
            var skills = new List<Skill>();
            foreach (var s in skillNames)
            {
                var norm = s.ToUpper();
                var existing = await db.Set<Skill>().FirstOrDefaultAsync(sk => sk.NormalizedName == norm);
                if (existing == null)
                {
                    existing = new Skill { Name = s, NormalizedName = norm };
                    db.Set<Skill>().Add(existing);
                }
                skills.Add(existing);
            }

            // 6. Create 15 Employees
            var firstNames = new[] { "Ahmed", "Sara", "Omar", "Mona", "Ali", "Nour", "Kareem", "Yara", "Khaled", "Reem", "Hassan", "Laila", "Tarek", "Salma", "Youssef" };
            var lastNames = new[] { "Ali", "Kamal", "Sayed", "Zaki", "Mahmoud", "Fahmy", "Hassan", "Gaber", "Nasser", "Samir", "Fouad", "Mostafa", "Sami", "Farid", "Taha" };
            var jobTitles = new[] { "Frontend Developer", "UI/UX Designer", "Backend Developer", "QA Engineer", "DevOps Engineer", "Full Stack Developer", "Data Scientist" };
            
            var newEmployees = new List<Employee>();
            for (int i = 0; i < 15; i++)
            {
                var email = $"{firstNames[i].ToLower()}.{lastNames[i].ToLower()}_{randomSuffix}@mock.taskpilot.com";
                var emp = new Employee 
                { 
                    UserName = email, Email = email, EmailConfirmed = true,
                    FirstNameEn = firstNames[i], LastNameEn = lastNames[i], JobTitle = jobTitles[i % jobTitles.Length], 
                    MaxSprintHours = 40, Company = company, IsProfileCompleted = true, TotalYearsOfExperience = (i%5)+1
                };
                
                var result = await userManager.CreateAsync(emp, "Password123!");
                if(result.Succeeded) {
                    await userManager.AddToRoleAsync(emp, empRole);
                    newEmployees.Add(emp);
                    db.Set<ProjectEmployee>().Add(new ProjectEmployee { Project = project, Employee = emp });
                    db.Set<UserSkill>().Add(new UserSkill { User = emp, Skill = skills[i % skills.Count], Level = SkillLevel.Intermediate, IsPrimary = true, ConfidenceScore = 0.8 });
                }
            }

            // 6. Create User Stories & Tasks & Comments
            var rnd = new Random();
            for (int i = 0; i < newEmployees.Count; i++)
            {
                var emp = newEmployees[i];
                var story = new UserStory { Project = project, Sprint = sprint, TitleEn = $"Story for {emp.FirstNameEn}'s Module", Priority = StoryPriority.High, Status = StoryStatus.InProgress };
                db.Set<UserStory>().Add(story);

                int riskRoll = rnd.Next(1, 100);
                int numTasks;
                
                if (riskRoll > 70) 
                {
                    numTasks = 4;
                    for(int t=0; t<numTasks; t++) {
                        db.Set<TaskItem>().Add(new TaskItem { Sprint = sprint, Employee = emp, UserStory = story, TitleEn = $"Heavy Task {t}", Status = TaskItemStatus.InProgress, EstimatedHours = 20, ActualHours = 25, Priority = TaskPriority.High });
                    }
                }
                else if (riskRoll > 40) 
                {
                    numTasks = 3;
                    for(int t=0; t<numTasks; t++) {
                        db.Set<TaskItem>().Add(new TaskItem { Sprint = sprint, Employee = emp, UserStory = story, TitleEn = $"Medium Task {t}", Status = TaskItemStatus.Review, EstimatedHours = 12, ActualHours = 14, Priority = TaskPriority.Medium });
                    }
                    db.Set<TaskComment>().Add(new TaskComment { User = emp, Content = "Need help with this." });
                }
                else 
                {
                    numTasks = 2;
                    for(int t=0; t<numTasks; t++) {
                        var task = new TaskItem { Sprint = sprint, Employee = emp, UserStory = story, TitleEn = $"Easy Task {t}", Status = TaskItemStatus.Done, EstimatedHours = 15, ActualHours = 12, Priority = TaskPriority.Low };
                        db.Set<TaskItem>().Add(task);
                        db.Set<TaskComment>().Add(new TaskComment { Task = task, User = emp, Content = "Finished earlier than expected!" });
                        db.Set<TaskComment>().Add(new TaskComment { Task = task, User = emp, Content = "Pushed changes to master." });
                    }
                }
            }

            await db.SaveChangesAsync();

            return Ok(new { 
                Message = $"تم إنشاء البيانات الخام بالكامل! قم بتسجيل الدخول بحساب الـ PM عشان تتست وتخلي الـ AI يحلل الداتا.", 
                PMAccount = pmEmail, 
                Password = "Password123!" 
            });
        }
    }
}
