using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.SubscriptionPlans;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common;

namespace TaskPilot.Presentation.Controllers
{
    [Route("api/[controller]")]
    public class SubscriptionPlansController : ApiControllerBase
    {
        private readonly ISubscriptionPlanService _subscriptionPlanService;
        private readonly IUnitOfWork _unitOfWork;

        public SubscriptionPlansController(ISubscriptionPlanService subscriptionPlanService, IUnitOfWork unitOfWork)
        {
            _subscriptionPlanService = subscriptionPlanService;
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        [Authorize(Roles = "ProjectManager,Admin")]
        public async Task<ActionResult> GetAll()
        {
            var result = await _subscriptionPlanService.GetAllAsync();
            return HandleResult(result);
        }
        [Authorize(Roles = "ProjectManager,Admin")]
        [HttpGet("{id:int}")]
        public async Task<ActionResult> GetById(int id)
        {
            var result = await _subscriptionPlanService.GetByIdAsync(id);
            return HandleResult(result);
        }
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult> Create([FromBody] CreateSubscriptionPlanDto request)
        {
            var result = await _subscriptionPlanService.CreateAsync(request);
            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleCreated(result, SuccessCodes.SubscriptionPlan.Created);
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Update(int id, [FromBody] UpdateSubscriptionPlanDto request)
        {
            var result = await _subscriptionPlanService.UpdateAsync(id, request);
            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result, SuccessCodes.SubscriptionPlan.Updated);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Delete(int id)
        {
            var result = await _subscriptionPlanService.DeleteAsync(id);
            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result, SuccessCodes.SubscriptionPlan.Deleted);
        }
    }
}
