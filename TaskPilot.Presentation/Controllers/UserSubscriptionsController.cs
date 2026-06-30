using System;
using TaskPilot.Models.Common;
using System.Threading.Tasks;
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

            return HandleResult(result, SuccessCodes.UserSubscription.Retrieved);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetAll([FromQuery] Guid? projectManagerId = null)
        {
            var result = await _userSubscriptionService.GetAllAsync(projectManagerId);
            return HandleResult(result, SuccessCodes.UserSubscription.Retrieved);
        }

        [HttpGet("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetById(Guid id)
        {
            var result = await _userSubscriptionService.GetByIdAsync(id);
            return HandleResult(result, SuccessCodes.UserSubscription.Retrieved);
        }

        [HttpPost]
        [Authorize(Roles = "ProjectManager")]
        public async Task<ActionResult> Subscribe([FromBody] CreateUserSubscriptionDto request)
        {
            var pmId = _currentUserService.UserId ?? Guid.Empty;
            var result = await _userSubscriptionService.CreateAsync(pmId, request);
            
            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleCreated(result, SuccessCodes.UserSubscription.Created);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Update(Guid id, [FromBody] UpdateUserSubscriptionDto request)
        {
            var result = await _userSubscriptionService.UpdateAsync(id, request);
            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result, SuccessCodes.UserSubscription.Updated);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Delete(Guid id)
        {
            var result = await _userSubscriptionService.DeleteAsync(id);
            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result, SuccessCodes.UserSubscription.Deleted);
        }
    }
}
