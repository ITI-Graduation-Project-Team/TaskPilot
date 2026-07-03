using TaskPilot.DTOs.CV;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces.CVExtractorInterfaces
{
    public interface ICvConfirmationService
    {
        Task<Result> ConfirmAsync(Guid userId, ConfirmCvRequest request);
    }
}
