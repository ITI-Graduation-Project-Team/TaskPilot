using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using TaskPilot.DTOs.Roles;
using TaskPilot.Models.Constants;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class RoleService : IRoleService
    {
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;

        public RoleService(RoleManager<IdentityRole<Guid>> roleManager)
        {
            _roleManager = roleManager;
        }

        public async Task<Result<List<RoleDto>>> GetAllRolesAsync()
        {
            var roles = _roleManager.Roles.ToList();
            var roleDtos = new List<RoleDto>();

            foreach (var role in roles)
            {
                var claims = await _roleManager.GetClaimsAsync(role);
                roleDtos.Add(new RoleDto
                {
                    Id = role.Id,
                    Name = role.Name,
                    Permissions = claims.Where(c => c.Type == "Permission").Select(c => c.Value).ToList()
                });
            }

            return Result<List<RoleDto>>.Success(roleDtos);
        }

        public async Task<Result<RoleDto>> GetRoleByIdAsync(Guid roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId.ToString());
            if (role == null)
            {
                return Result<RoleDto>.Failure(new Error("ROLE_NOT_FOUND", "Role not found", ErrorType.NotFound));
            }

            var claims = await _roleManager.GetClaimsAsync(role);
            var roleDto = new RoleDto
            {
                Id = role.Id,
                Name = role.Name,
                Permissions = claims.Where(c => c.Type == "Permission").Select(c => c.Value).ToList()
            };

            return Result<RoleDto>.Success(roleDto);
        }

        public Task<Result<List<PermissionModuleDto>>> GetPermissionMatrixAsync()
        {
            var matrix = new List<PermissionModuleDto>
            {
                new PermissionModuleDto
                {
                    ModuleName = "Projects",
                    Permissions = new List<PermissionItemDto>
                    {
                        new PermissionItemDto { Name = "View Projects", Value = Permissions.Projects.View },
                        new PermissionItemDto { Name = "Create Projects", Value = Permissions.Projects.Create },
                        new PermissionItemDto { Name = "Edit Projects", Value = Permissions.Projects.Edit },
                        new PermissionItemDto { Name = "Delete Projects", Value = Permissions.Projects.Delete }
                    }
                },
                new PermissionModuleDto
                {
                    ModuleName = "Users",
                    Permissions = new List<PermissionItemDto>
                    {
                        new PermissionItemDto { Name = "View Users", Value = Permissions.Users.View },
                        new PermissionItemDto { Name = "Create Users", Value = Permissions.Users.Create },
                        new PermissionItemDto { Name = "Edit Users", Value = Permissions.Users.Edit },
                        new PermissionItemDto { Name = "Delete Users", Value = Permissions.Users.Delete }
                    }
                },
                new PermissionModuleDto
                {
                    ModuleName = "Roles",
                    Permissions = new List<PermissionItemDto>
                    {
                        new PermissionItemDto { Name = "View Roles", Value = Permissions.Roles.View },
                        new PermissionItemDto { Name = "Create Roles", Value = Permissions.Roles.Create },
                        new PermissionItemDto { Name = "Edit Roles", Value = Permissions.Roles.Edit },
                        new PermissionItemDto { Name = "Delete Roles", Value = Permissions.Roles.Delete }
                    }
                },
                new PermissionModuleDto
                {
                    ModuleName = "Sprints",
                    Permissions = new List<PermissionItemDto>
                    {
                        new PermissionItemDto { Name = "View Sprints", Value = Permissions.Sprints.View },
                        new PermissionItemDto { Name = "Create Sprints", Value = Permissions.Sprints.Create },
                        new PermissionItemDto { Name = "Edit Sprints", Value = Permissions.Sprints.Edit },
                        new PermissionItemDto { Name = "Delete Sprints", Value = Permissions.Sprints.Delete }
                    }
                },
                new PermissionModuleDto
                {
                    ModuleName = "Tasks",
                    Permissions = new List<PermissionItemDto>
                    {
                        new PermissionItemDto { Name = "View Tasks", Value = Permissions.Tasks.View },
                        new PermissionItemDto { Name = "Create Tasks", Value = Permissions.Tasks.Create },
                        new PermissionItemDto { Name = "Edit Tasks", Value = Permissions.Tasks.Edit },
                        new PermissionItemDto { Name = "Delete Tasks", Value = Permissions.Tasks.Delete }
                    }
                }
            };

            return Task.FromResult(Result<List<PermissionModuleDto>>.Success(matrix));
        }

        public async Task<Result> UpdateRolePermissionsAsync(Guid roleId, UpdateRolePermissionsDto request)
        {
            var role = await _roleManager.FindByIdAsync(roleId.ToString());
            if (role == null)
            {
                return Result.Failure(new Error("ROLE_NOT_FOUND", "Role not found", ErrorType.NotFound));
            }

            var existingClaims = await _roleManager.GetClaimsAsync(role);
            var existingPermissions = existingClaims.Where(c => c.Type == "Permission").ToList();

            // Remove all existing permissions
            foreach (var claim in existingPermissions)
            {
                await _roleManager.RemoveClaimAsync(role, claim);
            }

            // Add new permissions
            foreach (var permission in request.Permissions)
            {
                await _roleManager.AddClaimAsync(role, new Claim("Permission", permission));
            }

            return Result.Success();
        }
    }
}
