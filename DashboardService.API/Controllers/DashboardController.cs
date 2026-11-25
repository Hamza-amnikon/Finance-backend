using SparkFinance.Http;

using System.ComponentModel.DataAnnotations;
using DashboardService.API.Data;
using DashboardService.API.DTOs;
using DashboardService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace DashboardService.API.Controllers
{
    [ApiController]
    [ServiceFilter(typeof(DashboardAccessFilter))]
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
                    cancellationToken,
                    GetBranches());

                return Ok(summary);
            }
            catch (ValidationException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
            catch (SqlException exception)
            {
                return DatabaseUnavailable(exception);
            }
            catch (MySqlException exception)
            {
                return MySqlUnavailable(exception);
            }
        }

        [HttpGet("activity")]
        [ProducesResponseType<List<DashboardActivityDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActivity(
            CancellationToken cancellationToken,
            [FromQuery] string? branch = null)
        {
            try
            {
                var activity = await _dashboardService.GetActivityAsync(
                    cancellationToken,
                    branch,
                    GetBranches());

                return Ok(activity);
            }
            catch (Exception exception)
                when (RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                return StatusCode(499);
            }
            catch (SqlException exception)
            {
                return DatabaseUnavailable(exception);
            }
            catch (MySqlException exception)
            {
                return MySqlUnavailable(exception);
            }
        }

        [HttpGet("branches")]
        public IActionResult GetAvailableBranches()
        {
            return Ok(GetBranches());
        }

        [HttpGet("currencies")]
        public async Task<IActionResult> GetCurrencies(
            [FromServices] DashboardDbContext database,
            CancellationToken cancellationToken)
        {
            var sql = database.IsMySql
                ? "SELECT DISTINCT CurrencyCode AS Value FROM sales__CurrencyMasters WHERE IsActive = 1 ORDER BY CurrencyCode"
                : "SELECT DISTINCT CurrencyCode AS Value FROM sales.CurrencyMasters WHERE IsActive = 1 ORDER BY CurrencyCode";
            var currencies = await database.Database.SqlQueryRaw<string>(sql)
                .ToListAsync(cancellationToken);
            return Ok(currencies.Where(code => code.Length == 3 && code.All(character => character is >= 'A' and <= 'Z')));
        }

        private IReadOnlyList<DashboardBranch> GetBranches()
        {
            return (IReadOnlyList<DashboardBranch>)HttpContext.Items[typeof(DashboardAccessFilter)]!;
        }

        private ObjectResult DatabaseUnavailable(SqlException exception)
        {
            _logger.LogWarning(
                exception,
                "Dashboard database request failed with SQL error {SqlErrorNumber}.",
                exception.Number);

            if (!exception.IsTransient && exception.Class < 20 && exception.Number != -2)
            {
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Dashboard data could not be loaded. Check the Dashboard Service logs.");
            }

            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The finance database is starting or temporarily unavailable. Retry shortly.");
        }

        private ObjectResult MySqlUnavailable(MySqlException exception)
        {
            _logger.LogWarning(exception, "Dashboard MySQL request failed with error {Number}.", exception.Number);
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The finance database could not serve the dashboard. Retry shortly.");
        }
    }
}
