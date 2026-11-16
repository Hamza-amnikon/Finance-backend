using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PermissionService.API.DTOs;
using PermissionService.API.Services;

namespace PermissionService.API.Controllers
{
    [ApiController]
    [Route("api/permissions")]
    public class PermissionsController : ControllerBase
    {
        private readonly IPermissionService _service;

        public PermissionsController(
            IPermissionService service)
        {
            _service = service;
        }

        [HttpGet]
        public Task<IActionResult> GetAll(
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var permissions = await _service.GetAllAsync(
                    cancellationToken);

                return Ok(permissions);
            });
        }

        [HttpGet("{permissionId:int:min(1)}")]
        public Task<IActionResult> GetById(
            int permissionId,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var permission = await _service.GetByIdAsync(
                    permissionId,
                    cancellationToken);

                if (permission == null)
                {
                    return NotFound(new
                    {
                        message = "Permission was not found."
                    });
                }

                return Ok(permission);
            });
        }

        [HttpPost]
        public Task<IActionResult> Create(
            [FromBody] PermissionRequest request,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var permission = await _service.CreateAsync(
                    request,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetById),
                    new
                    {
                        permissionId = permission.PermissionId
                    },
                    permission);
            });
        }

        [HttpPut("{permissionId:int:min(1)}")]
        public Task<IActionResult> Update(
            int permissionId,
            [FromBody] PermissionUpdateRequest request,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var permission = await _service.UpdateAsync(
                    permissionId,
                    request,
                    cancellationToken);

                if (permission == null)
                {
                    return NotFound(new
                    {
                        message = "Permission was not found."
                    });
                }

                return Ok(permission);
            });
        }

        private async Task<IActionResult> HandleAsync(
            Func<Task<IActionResult>> action)
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
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message =
                        "This permission was changed or removed by another user. " +
                        "Refresh the record before trying again."
                });
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException sqlException &&
                    (sqlException.Number == 2601 ||
                     sqlException.Number == 2627))
            {
                return Conflict(new
                {
                    message = "A permission with this code already exists."
                });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new
                {
                    message = exception.Message
                });
            }
        }
    }
}