using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Data.Context;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Enums;

namespace TaskPilot.Services.Implementations
{
    public class TokenQuotaEnforcer : ITokenQuotaEnforcer
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITokenQuotaContext _tokenQuotaContext;

        public TokenQuotaEnforcer(ApplicationDbContext dbContext, ICurrentUserService currentUserService, ITokenQuotaContext tokenQuotaContext)
        {
            _dbContext = dbContext;
            _currentUserService = currentUserService;
            _tokenQuotaContext = tokenQuotaContext;
        }

        private async Task<Guid?> GetProjectManagerIdAsync(CancellationToken cancellationToken)
        {
            var currentUserId = _currentUserService.UserId;
            if (!currentUserId.HasValue) return null;

            var user = await _dbContext.Users.Include(u => u.Company).FirstOrDefaultAsync(u => u.Id == currentUserId.Value, cancellationToken);
            if (user is ProjectManager) return user.Id;
            if (user?.Company != null) return user.Company.OwnerId;
            return null;
        }

        public async Task<(bool IsExceeded, long Limit, long CurrentUsage)> CheckQuotaAsync(CancellationToken cancellationToken = default)
        {
            var pmId = await GetProjectManagerIdAsync(cancellationToken);
            if (!pmId.HasValue) return (false, 0, 0);

            var pm = await _dbContext.Set<ProjectManager>().Where(p => p.Id == pmId.Value).FirstOrDefaultAsync(cancellationToken);
            var pmUsage = pm?.CurrentTokensUsedThisMonth ?? 0;

            var sub = await _dbContext.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.ProjectManagerId == pmId.Value && s.Status == SubscriptionStatus.Active)
                .FirstOrDefaultAsync(cancellationToken);
            var maxTokens = sub?.Plan?.MaxTokensPerMonth ?? 0;

            if (pmUsage >= maxTokens)
            {
                _tokenQuotaContext.LimitReached = true;
                _tokenQuotaContext.CurrentUsage = pmUsage;
                _tokenQuotaContext.Limit = maxTokens;
                return (true, maxTokens, pmUsage);
            }

            return (false, maxTokens, pmUsage);
        }

        public async Task TrackTokensAsync(ChatMessageContent response, CancellationToken cancellationToken = default)
        {
            var pmId = await GetProjectManagerIdAsync(cancellationToken);
            if (!pmId.HasValue) return;

            int promptTokens = 1500, completionTokens = 800;
            if (response.Metadata != null && response.Metadata.TryGetValue("Usage", out var usageObj) && usageObj != null)
            {
                var type = usageObj.GetType();
                var inProp = type.GetProperty("InputTokenCount") ?? type.GetProperty("PromptTokens");
                var outProp = type.GetProperty("OutputTokenCount") ?? type.GetProperty("CompletionTokens");
                if (inProp != null) promptTokens = Convert.ToInt32(inProp.GetValue(usageObj) ?? 0);
                if (outProp != null) completionTokens = Convert.ToInt32(outProp.GetValue(usageObj) ?? 0);
            }
            if (promptTokens == 0 && completionTokens == 0) { promptTokens = 1500; completionTokens = 800; }
            var total = promptTokens + completionTokens;

            await _dbContext.Set<ProjectManager>()
                .Where(p => p.Id == pmId.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.CurrentTokensUsedThisMonth, p => p.CurrentTokensUsedThisMonth + total), cancellationToken);
        }
    }
}
