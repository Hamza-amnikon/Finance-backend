using SparkFinance.Http;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using AccountingService.API.Data;
using AccountingService.API.Models;
using AccountingService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Controllers
{
    public class AuditHistoryQuery
    {
        [MaxLength(20)]
        public string? Activity { get; set; }

        public DateTimeOffset? From { get; set; }
        public DateTimeOffset? Before { get; set; }

        [MaxLength(200)]
        public string? Search { get; set; }

        [MaxLength(100)]
        public string? Service { get; set; }

        [MaxLength(100)]
        public string? EntityType { get; set; }

        [MaxLength(200)]
        public string? EntityId { get; set; }

        [MaxLength(100)]
        public string? Branch { get; set; }

        [MaxLength(40)]
        public string? Action { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 25;
    }

    [ApiController]
    [Route("api/audit-history")]
    public class AuditHistoryController : ControllerBase
    {
        private readonly AccountingDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AuditHistoryController> _logger;
        public AuditHistoryController(
            AccountingDbContext context,
            IWebHostEnvironment environment,
            ILogger<AuditHistoryController> logger
        )
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        private bool Allowed() => User.Identity?.IsAuthenticated == true ? User.IsInRole("Admin") : _environment.IsDevelopment() && Request.Headers["X-Audit-Role"] == "Admin";
        private async Task<IActionResult> Handle(Func<Task<IActionResult>> action)
        {
            if (!Allowed())
            {
                return StatusCode(
                                403,
                                new
                                {
                                    message = "Administrator access is required. Local role headers are accepted only in Development mode until authentication is connected."
                                }
                            );
            }

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
            catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (Exception cancellationException)
                when (RequestCancellation.IsCancellation(
                    cancellationException,
                    HttpContext?.RequestAborted ?? CancellationToken.None))
            {
                return StatusCode(499);
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Audit history database request failed.");
                return Problem(
                    statusCode: 503,
                    title: exception.Number == 208 ? "Audit History database setup is pending. Apply AddAuditHistory first." : "Audit history is unavailable. Check the finance database."
                );
            }
        }

        private IQueryable<AuditEvent> Filter(AuditHistoryQuery query)
        {
            if (query.From.HasValue && query.Before.HasValue && query.From >= query.Before)
            {
                throw new ValidationException("Choose a valid date range; From must be on or before To.");
            }

            var rows = AuditActivityFilter.Apply(_context, query.Activity);
            if (query.From.HasValue)
            {
                rows = rows.Where(row => row.AtUtc >= query.From.Value);
            }

            if (query.Before.HasValue)
            {
                rows = rows.Where(row => row.AtUtc < query.Before.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Service))
            {
                rows = rows.Where(row => row.Service == query.Service);
            }

            if (!string.IsNullOrWhiteSpace(query.EntityType))
            {
                rows = rows.Where(row => row.EntityType == query.EntityType);
            }

            if (!string.IsNullOrWhiteSpace(query.EntityId))
            {
                rows = rows.Where(row => row.EntityId == query.EntityId);
            }

            if (!string.IsNullOrWhiteSpace(query.Branch))
            {
                rows = rows.Where(row => row.Branch == query.Branch);
            }

            if (!string.IsNullOrWhiteSpace(query.Action))
            {
                rows = rows.Where(row => row.Action == query.Action);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                rows = rows.Where(row => row.EntityType.Contains(search) || row.EntityId.Contains(search) || row.Actor.Contains(search) || row.Action.Contains(search) || row.Service.Contains(search) || (row.Branch != null && row.Branch.Contains(search)) || (row.AfterJson != null && row.AfterJson.Contains(search)) || (row.BeforeJson != null && row.BeforeJson.Contains(search)));
            }

            return rows;
        }

        [HttpGet]
        public Task<IActionResult> Get(
            [FromQuery] AuditHistoryQuery query,
            CancellationToken cancellationToken
        ) => Handle(async () =>
        {
            var rows = Filter(query);
            var total = await rows.CountAsync(cancellationToken);
            var skip = ((long)query.Page - 1) * query.PageSize;
            if (skip > int.MaxValue)
            {
                throw new ValidationException("Page is too large.");
            }

            var data = await rows.OrderByDescending(row => row.Id).Skip((int)skip).Take(query.PageSize).ToListAsync(cancellationToken);
            return Ok(new
            {
                data,
                total,
                query.Page,
                query.PageSize
            });
        });
        [HttpGet("lookups")]
        public Task<IActionResult> Lookups([FromQuery] string? activity, CancellationToken cancellationToken) => Handle(async () =>
        {
            var rows = AuditActivityFilter.Apply(_context, activity);
            var savedBranches = await _context.Database
                .SqlQueryRaw<string>("SELECT Name AS Value FROM amnikon.Branches")
                .ToListAsync(cancellationToken);
            var metadata = await rows.Select(row => new
            {
                row.Service,
                row.EntityType,
                row.Action,
                row.Branch
            }).Distinct().ToListAsync(cancellationToken);
            var recordedBranches = metadata.Where(row => row.Branch != null)
                .Select(row => row.Branch!);

            return Ok(new
            {
                services = metadata.Select(row => row.Service).Distinct().Order().ToArray(),
                entityTypes = metadata.Select(row => row.EntityType).Distinct().Order().ToArray(),
                actions = metadata.Select(row => row.Action).Distinct().Order().ToArray(),
                branches = savedBranches.Concat(recordedBranches)
                    .Where(branch => !string.IsNullOrWhiteSpace(branch))
                    .Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray()
            });
        });
        [HttpGet("export")]
        public Task<IActionResult> Export(
            [FromQuery] AuditHistoryQuery query,
            CancellationToken cancellationToken
        ) => Handle(async () =>
        {
            var rows = await Filter(query).OrderByDescending(row => row.Id).Take(10001).ToListAsync(cancellationToken);
            if (rows.Count > 10000)
            {
                throw new ValidationException("Narrow your filters to export up to 10,000 audit events at a time.");
            }

            string Cell(string? text) => "\"" + (text ?? "").Replace("\"", "\"\"") + "\"";
            string Safe(string? value)
            {
                var text = value ?? "";
                if (text.TrimStart().Length > 0 && "=+@-".Contains(text.TrimStart()[0]))
                {
                    text = "'" + text;
                }

                return Cell(text);
            }

            var csv = new StringBuilder("\uFEFFTime (UTC),Service,Record,Record ID,Action,Actor,Identity type,Branch,Before,After\r\n");
            foreach (var row in rows)
            {
                csv.AppendLine(string.Join(
                ",",
                new[] { row.AtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture), row.Service, row.EntityType, row.EntityId, row.Action, row.Actor, row.ActorKind, row.Branch, row.BeforeJson, row.AfterJson }.Select(Safe)
            ));
            }

            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv;charset=utf-8", "audit-history.csv");
        });
    }
}
