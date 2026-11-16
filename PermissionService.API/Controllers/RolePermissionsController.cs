using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PermissionService.API.DTOs;
using PermissionService.API.Services;

namespace PermissionService.API.Controllers
{
    [ApiController]
    [Route("api/role-permissions")]
    [Authorize(Policy = "ManageRolePermissions")]
    public class RolePermissionsController : ControllerBase
    {
        private readonly IRolePermissionService _service;
        private readonly ILogger<RolePermissionsController> _logger;

        public RolePermissionsController(
            IRolePermissionService service,
            ILogger<RolePermissionsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("{roleId:int:min(1)}")]
        public Task<IActionResult> GetByRoleId(
            int roleId,
            CancellationToken cancellationToken)
        {
            return HandleAsync(
                async () =>
                {
                    var assignments = await _service.GetByRoleIdAsync(
                        roleId,
                        cancellationToken);

                    return Ok(assignments);
                },
                cancellationToken);
        }

        [HttpPut("{roleId:int:min(1)}")]
        public Task<IActionResult> Save(
            int roleId,
            [FromBody] SaveRolePermissionsRequest request,
            CancellationToken cancellationToken)
        {
            if (request.RoleId != roleId)
            {
                return Task.FromResult<IActionResult>(
                    BadRequest(new
                    {
                        message = "The role ID must match the URL."
                    }));
            }

            return HandleAsync(
                async () =>
                {
                    var assignments = await _service.SaveAsync(
                        request,
                        cancellationToken);

                    return Ok(assignments);
                },
                cancellationToken);
        }

        private async Task<IActionResult> HandleAsync(
            Func<Task<IActionResult>> action,
            CancellationToken cancellationToken)
        {
            try
            {
                return await action();
            }
            catch (ValidationException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
            catch (HttpRequestException exception)
            {
                _logger.LogError(
                    exception,
                    "Role Service could not be used to validate the selected role.");

                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        message =
                            "Role Service is unavailable or returned an unexpected response. " +
                            "Check that it is running and try again."
                    });
            }
            catch (OperationCanceledException exception)
                when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    exception,
                    "The role permission request timed out.");

                return StatusCode(
                    StatusCodes.Status504GatewayTimeout,
                    new
                    {
                        message =
                            "The request timed out. Refresh the role's permissions " +
                            "to check their saved state before trying again."
                    });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message =
                        "Permission assignments changed during this request. " +
                        "Refresh them before trying again."
                });
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException sqlException &&
                    (sqlException.Number == 2601 ||
                     sqlException.Number == 2627))
            {
                return Conflict(new
                {
                    message =
                        "A permission assignment already exists for this role. " +
                        "Refresh the assignments before trying again."
                });
            }
        }
    }
}