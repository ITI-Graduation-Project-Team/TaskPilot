using System;
using TaskPilot.Models.Common;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.DTOs.Roles;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [Route("api/[controller]")]
    // [Authorize(Roles = "Admin")]
    public class RolesController : ApiControllerBase
    {
        private readonly IRoleService _roleService;

        public RolesController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllRoles()
        {
            var result = await _roleService.GetAllRolesAsync();
            return HandleResult(result, SuccessCodes.Role.Retrieved);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetRoleById(Guid id)
        {
            var result = await _roleService.GetRoleByIdAsync(id);
            return HandleResult(result, SuccessCodes.Role.Retrieved);
        }

        [HttpGet("permissions-matrix")]
        public async Task<IActionResult> GetPermissionMatrix()
        {
            var result = await _roleService.GetPermissionMatrixAsync();
            return HandleResult(result, SuccessCodes.Role.Retrieved);
        }

        [HttpPut("{id}/permissions")]
        public async Task<IActionResult> UpdateRolePermissions(Guid id, [FromBody] UpdateRolePermissionsDto request)
        {
            var result = await _roleService.UpdateRolePermissionsAsync(id, request);
            return HandleResult(result, SuccessCodes.Role.PermissionsUpdated);
        }
    }
}
