using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Data;
using TaskPilot.Data.Context;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces.External;
using TaskPilot.DTOs;
using Microsoft.Extensions.Logging;

namespace TaskPilot.Services.BackgroundJobs
{
    public class SprintEndReminderJob
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IEmailService _emailService;
        private readonly ILogger<SprintEndReminderJob> _logger;

        public SprintEndReminderJob(
            ApplicationDbContext dbContext,
            IEmailService emailService,
            ILogger<SprintEndReminderJob> logger)
        {
            _dbContext = dbContext;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task ExecuteAsync(Guid sprintId)
        {
            _logger.LogInformation("Executing SprintEndReminderJob for Sprint {SprintId}", sprintId);

            var sprint = await _dbContext.Sprints
                .Include(s => s.Project)
                    .ThenInclude(p => p.Manager)
                .Include(s => s.Tasks)
                .FirstOrDefaultAsync(s => s.Id == sprintId);

            if (sprint == null || sprint.Status == SprintStatus.Completed)
            {
                _logger.LogInformation("Sprint {SprintId} is missing or already completed.", sprintId);
                return;
            }

            var projectManager = sprint.Project?.Manager;
            if (projectManager == null || string.IsNullOrEmpty(projectManager.Email))
            {
                _logger.LogWarning("Project manager for Sprint {SprintId} is missing or has no email.", sprintId);
                return;
            }

            var tasks = sprint.Tasks.ToList();
            var totalTasks = tasks.Count;
            var doneTasks = tasks.Count(t => t.Status == TaskItemStatus.Done);
            var inProgressTasks = tasks.Count(t => t.Status == TaskItemStatus.InProgress);
            var reviewTasks = tasks.Count(t => t.Status == TaskItemStatus.Review);
            var todoTasks = tasks.Count(t => t.Status == TaskItemStatus.ToDo);

            var stuckTasks = tasks.Where(t => t.Status == TaskItemStatus.InProgress || t.Status == TaskItemStatus.Review).ToList();

            var subject = $"TaskPilot: Sprint Ending Soon - {sprint.TitleEn}";
            
            var body = $@"
<h2>Sprint Reminder: {sprint.TitleEn} ends tomorrow!</h2>
<p>Project: {sprint.Project?.NameEn}</p>
<hr/>
<h3>Sprint Statistics</h3>
<ul>
    <li>Total Tasks: {totalTasks}</li>
    <li>Done: {doneTasks}</li>
    <li>In Progress: {inProgressTasks}</li>
    <li>In Review: {reviewTasks}</li>
    <li>To Do: {todoTasks}</li>
</ul>
<br/>
";
            if (stuckTasks.Any())
            {
                body += "<h3>Tasks requiring attention (In Progress or Review):</h3><ul>";
                foreach (var task in stuckTasks)
                {
                    body += $"<li><b>{task.TitleEn}</b> - <i>{task.Status}</i></li>";
                }
                body += "</ul>";
            }
            else
            {
                body += "<p>Great job! No tasks are stuck in progress or review.</p>";
            }

            var emailRequest = new EmailRequest
            {
                To = projectManager.Email,
                Subject = subject,
                Body = body
            };

            var result = await _emailService.SendEmailAsync(emailRequest);
            if (!result.IsSuccess)
            {
                _logger.LogError("Failed to send sprint reminder email for Sprint {SprintId}", sprintId);
            }
            else
            {
                _logger.LogInformation("Successfully sent sprint reminder email for Sprint {SprintId} to {Email}", sprintId, projectManager.Email);
            }
        }
    }
}
