using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Employees;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Interfaces.ExternalServicesInterfaces;

namespace TaskPilot.Services.Implementations
{
    public class EmployeeProfileService : IEmployeeProfileService
    {
        private readonly IRepository<User> _userRepository;
        private readonly IFileStorageService _fileStorage;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEntitlementService _entitlementService;

        public EmployeeProfileService(
            IRepository<User> userRepository,
            IFileStorageService fileStorage,
            IUnitOfWork unitOfWork,
            IEntitlementService entitlementService)
        {
            _userRepository = userRepository;
            _fileStorage = fileStorage;
            _unitOfWork = unitOfWork;
            _entitlementService = entitlementService;
        }

        public async Task<Result> UpdateProfileAsync(Guid userId, UpdateEmployeeProfileDto request)
        {
            var user = await _userRepository.GetQueryable()
                .Include(u => u.Company)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return Result.Failure(CommonErrors.NotFound("User not found."));
            }

            // 1. Update Common User Fields
            user.FirstNameEn = request.FirstNameEn.Trim();
            user.LastNameEn = request.LastNameEn.Trim();
            user.FirstNameAr = request.FirstNameAr?.Trim() ?? string.Empty;
            user.LastNameAr = request.LastNameAr?.Trim() ?? string.Empty;
            
            // Only update PhoneNumber if it's an employee (just using the user table, IdentityUser already has PhoneNumber)
            if (!string.IsNullOrEmpty(request.PhoneNumber))
            {
                user.PhoneNumber = request.PhoneNumber.Trim();
            }

            // 2. Avatar Processing
            if (request.DeleteAvatar)
            {
                if (user.AvatarFileSize > 0 && user.Company != null)
                {
                    await _entitlementService.UpdateStorageUsageAsync(user.Company.OwnerId, -user.AvatarFileSize);
                    user.AvatarFileSize = 0;
                }
                
                if (!string.IsNullOrEmpty(user.AvatarPublicId))
                {
                    await _fileStorage.DeleteFileAsync(user.AvatarPublicId);
                    user.AvatarPublicId = null;
                }
                user.AvatarUrl = $"https://api.dicebear.com/9.x/initials/svg?seed={Uri.EscapeDataString(user.FirstNameEn + " " + user.LastNameEn)}";
            }
            else if (request.Avatar != null && request.Avatar.Length > 0)
            {
                if (user.Company != null)
                {
                    var entitlementResult = await _entitlementService.EnsureCanUploadAsync(user.Company.OwnerId, request.Avatar.Length, user.AvatarFileSize);
                    if (entitlementResult.IsFailure) return Result.Failure(entitlementResult.Error);
                }

                // Delete old avatar if exists
                if (!string.IsNullOrEmpty(user.AvatarPublicId))
                {
                    await _fileStorage.DeleteFileAsync(user.AvatarPublicId);
                }

                // Upload new avatar
                var avatarUploadResult = await _fileStorage.UploadFileAsync(request.Avatar, $"taskpilot/users/{user.Id}/avatars");
                if (!avatarUploadResult.IsSuccess)
                {
                    return Result.Failure(avatarUploadResult.Error!);
                }

                user.AvatarUrl = avatarUploadResult.Value.Url;
                user.AvatarPublicId = avatarUploadResult.Value.PublicId;
                
                if (user.Company != null)
                {
                    await _entitlementService.UpdateStorageUsageAsync(user.Company.OwnerId, request.Avatar.Length - user.AvatarFileSize);
                }
                user.AvatarFileSize = request.Avatar.Length;
            }
            else if (string.IsNullOrEmpty(user.AvatarUrl) || user.AvatarUrl.StartsWith("https://api.dicebear.com"))
            {
                // Re-generate Avatar using DiceBear if none exists or if it's already dicebear (in case name changed)
                user.AvatarUrl = $"https://api.dicebear.com/9.x/initials/svg?seed={Uri.EscapeDataString(user.FirstNameEn + " " + user.LastNameEn)}";
                
                if (user.AvatarFileSize > 0 && user.Company != null)
                {
                    await _entitlementService.UpdateStorageUsageAsync(user.Company.OwnerId, -user.AvatarFileSize);
                    user.AvatarFileSize = 0;
                }
            }

            // 3. Employee-Specific Processing
            if (user is Employee employee)
            {
                employee.JobTitle = request.JobTitle?.Trim();
                employee.SeniorityLevel = request.SeniorityLevel;
                employee.TotalYearsOfExperience = request.TotalYearsOfExperience;

                // CV Processing
                if (request.CvFile != null && request.CvFile.Length > 0)
                {
                    if (user.Company != null)
                    {
                        var entitlementResult = await _entitlementService.EnsureCanUploadAsync(user.Company.OwnerId, request.CvFile.Length, employee.CvFileSize);
                        if (entitlementResult.IsFailure) return Result.Failure(entitlementResult.Error);
                    }

                    // Delete old CV if exists
                    if (!string.IsNullOrEmpty(employee.CvPublicId))
                    {
                        await _fileStorage.DeleteFileAsync(employee.CvPublicId);
                    }

                    // Upload new CV
                    var cvUploadResult = await _fileStorage.UploadFileAsync(request.CvFile, $"taskpilot/employees/{employee.Id}/cvs");
                    if (!cvUploadResult.IsSuccess)
                    {
                        return Result.Failure(cvUploadResult.Error!);
                    }

                    employee.LatestCvUrl = cvUploadResult.Value.Url;
                    employee.CvPublicId = cvUploadResult.Value.PublicId;
                    
                    if (user.Company != null)
                    {
                        await _entitlementService.UpdateStorageUsageAsync(user.Company.OwnerId, request.CvFile.Length - employee.CvFileSize);
                    }
                    employee.CvFileSize = request.CvFile.Length;
                    
                    // Trigger AI extraction by setting status to Pending
                    employee.CvProcessingStatus = AiProcessingStatus.Pending;
                }

                // Check Profile Completion
                if (!string.IsNullOrEmpty(employee.FirstNameEn) &&
                    !string.IsNullOrEmpty(employee.LastNameEn) &&
                    !string.IsNullOrEmpty(employee.JobTitle) &&
                    employee.TotalYearsOfExperience.HasValue)
                {
                    employee.IsProfileCompleted = true;
                }
            }

            user.ModifiedAt = DateTime.UtcNow;

            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }
    }
}
