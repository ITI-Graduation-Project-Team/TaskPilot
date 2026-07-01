using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.UserSubscriptions;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class UserSubscriptionsController : ApiControllerBase
    {
        private readonly IUserSubscriptionService _userSubscriptionService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;

        public UserSubscriptionsController(
            IUserSubscriptionService userSubscriptionService,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService)
        {
            _userSubscriptionService = userSubscriptionService;
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
        }

        [HttpGet("current")]
        [Authorize(Roles = "ProjectManager,Admin")]
        public async Task<ActionResult> GetCurrentSubscription([FromQuery] Guid? projectManagerId = null)
        {
            // If PM, they can only view their own. Admin can view anyone's by passing the ID.
            var pmId = User.IsInRole("Admin") && projectManagerId.HasValue
                ? projectManagerId.Value
                : _currentUserService.UserId ?? Guid.Empty;

            var result = await _userSubscriptionService.GetCurrentSubscriptionAsync(pmId);

            // GetCurrentSubscriptionAsync might mutate state (auto-fallback to free), so we save changes just in case.
            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetAll([FromQuery] Guid? projectManagerId = null)
        {
            var result = await _userSubscriptionService.GetAllAsync(projectManagerId);
            return HandleResult(result);
        }

        [HttpGet("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetById(Guid id)
        {
            var result = await _userSubscriptionService.GetByIdAsync(id);
            return HandleResult(result);
        }

        [HttpPost]
        [Authorize(Roles = "ProjectManager")]
        public async Task<ActionResult> Subscribe([FromBody] CreateUserSubscriptionDto request)
        {
            var pmId = _currentUserService.UserId ?? Guid.Empty;
            var result = await _userSubscriptionService.CreateAsync(pmId, request);

            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleCreated(result, "Subscribed successfully.");
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Update(Guid id, [FromBody] UpdateUserSubscriptionDto request)
        {
            var result = await _userSubscriptionService.UpdateAsync(id, request);
            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result, "Subscription updated successfully.");
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Delete(Guid id)
        {
            var result = await _userSubscriptionService.DeleteAsync(id);
            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result, "Subscription deleted successfully.");
        }

        [HttpPost("{id:guid}/cancel")]
        [Authorize(Roles = "ProjectManager")]
        public async Task<ActionResult> Cancel(Guid id)
        {
            // First check if the subscription belongs to the current user
            var pmId = _currentUserService.UserId ?? Guid.Empty;
            var subResult = await _userSubscriptionService.GetByIdAsync(id);

            if (!subResult.IsSuccess)
                return HandleResult(subResult);

            if (subResult.Value.ProjectManagerId != pmId)
                return Forbid();

            var result = await _userSubscriptionService.CancelAsync(id, pmId);
            
            return HandleResult(result, "Subscription cancelled successfully.");
        }
    }
}
