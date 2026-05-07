using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskPilot.DTOs.Roles;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IRoleService
    {
        Task<Result<List<RoleDto>>> GetAllRolesAsync();
        Task<Result<RoleDto>> GetRoleByIdAsync(Guid roleId);
        Task<Result<List<PermissionModuleDto>>> GetPermissionMatrixAsync();
        Task<Result> UpdateRolePermissionsAsync(Guid roleId, UpdateRolePermissionsDto request);
    }
}
