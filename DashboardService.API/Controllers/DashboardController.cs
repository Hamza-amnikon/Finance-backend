
using System.ComponentModel.DataAnnotations;
using DashboardService.API.DTOs;
using DashboardService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace DashboardService.API.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            IDashboardService dashboardService,
            ILogger<DashboardController> logger)
        {
            _dashboardService = dashboardService;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType<DashboardSummaryDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetSummary(
            [FromQuery, Required] DateOnly from,
            [FromQuery, Required] DateOnly to,
            [FromQuery] string branch = "",
            [FromQuery, Required, RegularExpression("^[A-Za-z]{3}$")] string currency = "INR",
            CancellationToken cancellationToken = default)
        {
            try
            {
                var summary = await _dashboardService.GetSummaryAsync(
                    from,
                    to,
                    branch,
                    currency,
                    cancellationToken);

                return Ok(summary);
            }
            catch (ValidationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (SqlException exception)
            {
                return DatabaseUnavailable(exception);
            }
        }

        [HttpGet("activity")]
        [ProducesResponseType<List<DashboardActivityDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActivity(
            CancellationToken cancellationToken)
        {
            try
            {
                var activity = await _dashboardService.GetActivityAsync(
                    cancellationToken);

                return Ok(activity);
            }
            catch (SqlException exception)
            {
                return DatabaseUnavailable(exception);
            }
        }

        private ObjectResult DatabaseUnavailable(SqlException exception)
        {
            _logger.LogWarning(
                exception,
                "The dashboard could not reach the finance database.");

            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The finance database is starting or temporarily unavailable. Retry shortly.");
        }
    }
}
