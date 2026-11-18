using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoleService.API.DTOs;
using RoleService.API.Services;

namespace RoleService.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = DevelopmentAdminEndpoints.AdminRole)]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;

        public UsersController(
            IUserService service)
        {
            _service = service;
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

        [HttpPut("{userId:guid}/microsoft-identity")]
        public Task<IActionResult> UpdateMicrosoftIdentity(
            Guid userId,
            [FromBody] UpdateMicrosoftIdentityRequest request,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
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
