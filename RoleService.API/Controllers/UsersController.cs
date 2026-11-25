using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoleService.API.Authentication;
using RoleService.API.Data;
using RoleService.API.DTOs;
using RoleService.API.Models;
using RoleService.API.Services;

namespace RoleService.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    [ServiceFilter(typeof(UsersPermissionFilter))]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;
        private readonly IRoleService _roles;
        private readonly RoleDbContext _context;

        public UsersController(
            IUserService service,
            IRoleService roles,
            RoleDbContext context)
        {
            _service = service;
            _roles = roles;
            _context = context;
        }

        [HttpGet]
        public Task<IActionResult> GetUsers(
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var users = await _service.GetAllAsync(
                    cancellationToken);

                return Ok(users);
            });
        }

        [HttpGet("{userId:guid}")]
        public Task<IActionResult> GetUser(
            Guid userId,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var user = await _service.GetByIdAsync(
                    userId,
                    cancellationToken);

                if (user == null)
                {
                    return NotFound(new
                    {
                        message = "The user was not found."
                    });
                }

                return Ok(user);
            });
        }

        [HttpPost]
        public Task<IActionResult> CreateUser(
            [FromBody] CreateUserRequest request,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                if (!await CanChangeUserAsync(request.RoleId, null, cancellationToken))
                {
                    return Forbid();
                }

                var (result, user) = await _service.CreateAsync(
                    request,
                    cancellationToken);

                if (result != UserChangeResult.Success)
                {
                    return GetChangeResponse(result);
                }

                if (user == null)
                {
                    throw new InvalidOperationException(
                        "The service did not return a created user.");
                }

                return CreatedAtAction(
                    nameof(GetUser),
                    new
                    {
                        userId = user.UserId
                    },
                    user);
            });
        }

        [HttpPut("{userId:guid}")]
        public Task<IActionResult> UpdateUser(
            Guid userId,
            [FromBody] UpdateUserRequest request,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                // UsersPermissionFilter requires Users Edit for every role assignment.
                var (result, user) = await _service.UpdateAsync(
                    userId,
                    request,
                    cancellationToken);

                if (result != UserChangeResult.Success)
                {
                    return GetChangeResponse(result);
                }

                if (user == null)
                {
                    throw new InvalidOperationException(
                        "The service did not return an updated user.");
                }

                return Ok(user);
            });
        }

        [Authorize(Roles = DevelopmentAdminEndpoints.AdminRole)]
        [HttpPut("{userId:guid}/microsoft-identity")]
        public Task<IActionResult> UpdateMicrosoftIdentity(
            Guid userId,
            [FromBody] UpdateMicrosoftIdentityRequest request,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var existing = await _service.GetByIdAsync(userId, cancellationToken);
                if (existing != null &&
                    !await CanChangeUserAsync(existing.RoleId, userId, cancellationToken))
                {
                    return Forbid();
                }

                var (result, user) = await _service.UpdateMicrosoftIdentityAsync(
                    userId,
                    request,
                    cancellationToken);

                if (result != UserChangeResult.Success)
                {
                    return GetChangeResponse(result);
                }

                if (user == null)
                {
                    throw new InvalidOperationException(
                        "The service did not return an updated user.");
                }

                return Ok(user);
            });
        }

        private async Task<bool> CanChangeUserAsync(
            int roleId,
            Guid? userId,
            CancellationToken cancellationToken)
        {
            if (User.IsInRole(DevelopmentAdminEndpoints.AdminRole))
            {
                return true;
            }

            var role = await _roles.GetByIdAsync(roleId, cancellationToken);
            if (role?.Role == DevelopmentAdminEndpoints.AdminRole)
            {
                return false;
            }

            if (userId.HasValue)
            {
                var existing = await _service.GetByIdAsync(userId.Value, cancellationToken);
                if (existing?.Role == DevelopmentAdminEndpoints.AdminRole)
                {
                    return false;
                }
            }

            if (!int.TryParse(User.FindFirst("role_id")?.Value, out var actorRoleId))
            {
                return false;
            }

            var permissionSql = _context.IsMySql ? """
                SELECT EXISTS (
                    SELECT 1 FROM security__RolePermissions target
                    LEFT JOIN security__RolePermissions actor
                        ON actor.RoleId = {0} AND actor.CompanyId = target.CompanyId
                        AND actor.PermissionId = target.PermissionId
                    WHERE target.RoleId = {1} AND (
                        (target.CanView = 1 AND COALESCE(actor.CanView, 0) = 0) OR
                        (target.CanCreate = 1 AND COALESCE(actor.CanCreate, 0) = 0) OR
                        (target.CanEdit = 1 AND COALESCE(actor.CanEdit, 0) = 0) OR
                        (target.CanDelete = 1 AND COALESCE(actor.CanDelete, 0) = 0) OR
                        (target.CanApprove = 1 AND COALESCE(actor.CanApprove, 0) = 0) OR
                        (target.CanExport = 1 AND COALESCE(actor.CanExport, 0) = 0) OR
                        (target.CanCancel = 1 AND COALESCE(actor.CanCancel, 0) = 0) OR
                        (target.CanIssue = 1 AND COALESCE(actor.CanIssue, 0) = 0) OR
                        (target.CanPost = 1 AND COALESCE(actor.CanPost, 0) = 0) OR
                        (target.CanVoid = 1 AND COALESCE(actor.CanVoid, 0) = 0) OR
                        (target.CanReverse = 1 AND COALESCE(actor.CanReverse, 0) = 0) OR
                        (target.CanReconcile = 1 AND COALESCE(actor.CanReconcile, 0) = 0)
                    )
                ) AS Value
                """ : """
                SELECT CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM security.RolePermissions target
                    LEFT JOIN security.RolePermissions actor
                        ON actor.RoleId = {0} AND actor.CompanyId = target.CompanyId
                        AND actor.PermissionId = target.PermissionId
                    WHERE target.RoleId = {1} AND (
                        (target.CanView = 1 AND ISNULL(actor.CanView, 0) = 0) OR
                        (target.CanCreate = 1 AND ISNULL(actor.CanCreate, 0) = 0) OR
                        (target.CanEdit = 1 AND ISNULL(actor.CanEdit, 0) = 0) OR
                        (target.CanDelete = 1 AND ISNULL(actor.CanDelete, 0) = 0) OR
                        (target.CanApprove = 1 AND ISNULL(actor.CanApprove, 0) = 0) OR
                        (target.CanExport = 1 AND ISNULL(actor.CanExport, 0) = 0) OR
                        (target.CanCancel = 1 AND ISNULL(actor.CanCancel, 0) = 0) OR
                        (target.CanIssue = 1 AND ISNULL(actor.CanIssue, 0) = 0) OR
                        (target.CanPost = 1 AND ISNULL(actor.CanPost, 0) = 0) OR
                        (target.CanVoid = 1 AND ISNULL(actor.CanVoid, 0) = 0) OR
                        (target.CanReverse = 1 AND ISNULL(actor.CanReverse, 0) = 0) OR
                        (target.CanReconcile = 1 AND ISNULL(actor.CanReconcile, 0) = 0)
                    )
                ) THEN 1 ELSE 0 END AS int) AS Value
                """;
            var exceedsActorAccess = await _context.Database.SqlQueryRaw<int>(
                permissionSql, actorRoleId, roleId).SingleAsync(cancellationToken);

            return exceedsActorAccess == 0;
        }

        private IActionResult GetChangeResponse(
            UserChangeResult result)
        {
            return result switch
            {
                UserChangeResult.NotFound =>
                    NotFound(new
                    {
                        message = "The user was not found."
                    }),
                UserChangeResult.DuplicateIdentity =>
                    Conflict(new
                    {
                        message = "A user with this Microsoft identity already exists."
                    }),
                UserChangeResult.ConcurrencyConflict =>
                    Conflict(new
                    {
                        message = "The user changed. Reload the user and try again."
                    }),
                _ => throw new InvalidOperationException(
                    "Unexpected user change result.")
            };
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
        }
    }
}
