using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.SubscriptionPlans;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [Authorize(Roles = "Admin")]
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
        public async Task<ActionResult> GetAll()
        {
            var result = await _subscriptionPlanService.GetAllAsync();
            return HandleResult(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult> GetById(int id)
        {
            var result = await _subscriptionPlanService.GetByIdAsync(id);
            return HandleResult(result);
        }

        [HttpPost]
        public async Task<ActionResult> Create([FromBody] CreateSubscriptionPlanDto request)
        {
            var result = await _subscriptionPlanService.CreateAsync(request);
            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleCreated(result, "Subscription plan created successfully.");
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult> Update(int id, [FromBody] UpdateSubscriptionPlanDto request)
        {
            var result = await _subscriptionPlanService.UpdateAsync(id, request);
            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result, "Subscription plan updated successfully.");
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Delete(int id)
        {
            var result = await _subscriptionPlanService.DeleteAsync(id);
            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result, "Subscription plan deleted successfully.");
        }
    }
}
