using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoleService.API.DTOs;
using RoleService.API.Models;
using RoleService.API.Services;

namespace RoleService.API.Controllers
{
    [ApiController]
    [Route("api/role")]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _service;

        public RoleController(IRoleService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<List<Roles>>> GetRoles(
            CancellationToken cancellationToken)
        {
            var roles = await _service.GetAllAsync(cancellationToken);

            return Ok(roles);
        }

        [HttpGet("statuses")]
        public IActionResult GetStatuses()
        {
            return Ok(new
            {
                statuses = new[] { RoleStatus.Active, RoleStatus.Inactive },
                defaultStatus = RoleStatus.Active
            });
        }

        [HttpGet("active")]
        public async Task<ActionResult<List<Roles>>> GetActiveRoles(
            CancellationToken cancellationToken)
        {
            var roles = await _service.GetActiveAsync(cancellationToken);

            return Ok(roles);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Roles>> GetRole(
            int id,
            CancellationToken cancellationToken)
        {
            var role = await _service.GetByIdAsync(id, cancellationToken);

            if (role is null)
            {
                return NotFound();
            }

            return Ok(role);
        }

        [Authorize(Roles = DevelopmentAdminEndpoints.AdminRole)]
        [HttpPost]
        public async Task<ActionResult<Roles>> CreateRole(
            RoleRequest request,
            CancellationToken cancellationToken)
        {
            var (result, role) = await _service.CreateAsync(
                request,
                cancellationToken);

            if (result == RoleChangeResult.DuplicateName)
            {
                return Conflict(new
                {
                    message = "A role with this name already exists."
                });
            }

            if (role is null)
            {
                throw new InvalidOperationException("The service did not return a created role.");
            }

            return CreatedAtAction(nameof(GetRole), new
            {
                id = role.RoleId
            }, role);
        }

        [Authorize(Roles = DevelopmentAdminEndpoints.AdminRole)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateRole(
            int id,
            RoleRequest request,
            CancellationToken cancellationToken)
        {
            if (request.RoleId.HasValue && request.RoleId.Value != id)
            {
                return BadRequest(new
                {
                    message = "The role ID must match the URL."
                });
            }

            if (request.RowVersion is not { Length: 8 })
            {
                return BadRequest(new
                {
                    message = "Include the role's current row version."
                });
            }

            var result = await _service.UpdateAsync(
                id,
                request,
                request.RowVersion,
                cancellationToken);

            return GetChangeResponse(result);
        }

        [Authorize(Roles = DevelopmentAdminEndpoints.AdminRole)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteRole(
            int id,
            CancellationToken cancellationToken)
        {
            var result = await _service.DeleteAsync(id, cancellationToken);

            return GetChangeResponse(result);
        }

        private IActionResult GetChangeResponse(RoleChangeResult result)
        {
            return result switch
            {
                RoleChangeResult.Success => NoContent(),
                RoleChangeResult.NotFound => NotFound(),
                RoleChangeResult.DuplicateName =>
                    Conflict(new
                    {
                        message = "A role with this name already exists."
                    }),
                RoleChangeResult.ConcurrencyConflict =>
                    Conflict(new
                    {
                        message = "The role changed. Reload it and try again."
                    }),
                _ => throw new InvalidOperationException("Unexpected role change result.")
            };
        }
    }
}
