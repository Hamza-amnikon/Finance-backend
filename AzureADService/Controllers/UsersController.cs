using System.ComponentModel.DataAnnotations;
using Azure.Identity;
using AzureADService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Kiota.Abstractions;

namespace AzureADService.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Policy = "ManageDirectoryUsers")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class UsersController : ControllerBase
    {
        private readonly AzureGraphService _service;

        public UsersController(AzureGraphService service)
        {
            _service = service;
        }

        [HttpGet("search")]
        public Task<IActionResult> Search(
            [FromQuery, Required, EmailAddress, MaxLength(254)] string workAccount,
            CancellationToken cancellationToken)
        {
            return LookupAsync(workAccount.Trim(), cancellationToken);
        }

        [HttpGet("{objectId:guid}")]
        public Task<IActionResult> GetUser(
            Guid objectId,
            CancellationToken cancellationToken)
        {
            return LookupAsync(objectId.ToString(), cancellationToken);
        }

        private async Task<IActionResult> LookupAsync(
            string identity,
            CancellationToken cancellationToken)
        {
            if (!_service.IsConfigured)
            {
                return Problem(
                    detail: "Configure the MERIT directory credentials in server configuration.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            try
            {
                var result = await _service.GetUserAsync(identity, cancellationToken);

                if (result == null)
                {
                    return NotFound();
                }

                return Ok(result);
            }
            catch (ApiException exception) when (exception.ResponseStatusCode == 404)
            {
                return NotFound();
            }
            catch (Exception exception) when (
                exception is ApiException or AuthenticationFailedException or HttpRequestException ||
                exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                return Problem(
                    detail: "Microsoft directory lookup failed. Check the application credentials and Graph permissions.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }
    }
}
