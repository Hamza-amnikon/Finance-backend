using System.ComponentModel.DataAnnotations;
using ExpenseService.API.Data;
using ExpenseService.API.DTOs;
using ExpenseService.API.Models;
using ExpenseService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ExpenseService.API.Controllers
{
    [ApiController]
    [Route("api/expenses")]
    public class ExpensesController : ControllerBase
    {
        private readonly IExpenseService _service;
        private readonly IExpenseLookupService _lookups;
        private readonly ExpenseDbContext _context;
        private readonly ILogger<ExpensesController> _logger;

        public ExpensesController(
            IExpenseService service,
            IExpenseLookupService lookups,
            ExpenseDbContext context,
            ILogger<ExpensesController> logger)
        {
            _service = service;
            _lookups = lookups;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public Task<IActionResult> GetPaged(
            [FromQuery] ExpenseQueryDto query,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var result = await _service.GetPagedAsync(
                    query,
                    cancellationToken);

                return Ok(result);
            }, cancellationToken);
        }

        [HttpGet("{id:int}")]
        public Task<IActionResult> GetById(
            int id,
            CancellationToken cancellationToken,
            [FromQuery, RegularExpression("^[A-Z]{3}$")]
            string? displayCurrency = null)
        {
            return HandleAsync(async () =>
            {
                var expense = await _service.GetByIdAsync(
                    id,
                    displayCurrency,
                    cancellationToken);

                return Ok(expense);
            }, cancellationToken);
        }

        [HttpPost]
        public Task<IActionResult> Create(
            [FromBody] ExpenseDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var expense = await _service.SaveAsync(
                    null,
                    dto,
                    cancellationToken);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = expense.Id },
                    expense);
            }, cancellationToken);
        }

        [HttpPut("{id:int}")]
        public Task<IActionResult> Update(
            int id,
            [FromBody] ExpenseDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var expense = await _service.SaveAsync(
                    id,
                    dto,
                    cancellationToken);

                return Ok(expense);
            }, cancellationToken);
        }

        [HttpPost("{id:int}/void")]
        public Task<IActionResult> Void(
            int id,
            [FromBody] VoidExpenseDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var expense = await _service.VoidAsync(
                    id,
                    dto,
                    cancellationToken);

                return Ok(expense);
            }, cancellationToken);
        }

        [HttpGet("lookups/countries")]
        public Task<IActionResult> Countries(
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var countries = await _lookups.GetCountriesAsync(cancellationToken);

                return Ok(countries);
            }, cancellationToken);
        }

        [HttpGet("lookups/currencies")]
        public Task<IActionResult> Currencies(
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var currencies = await _lookups.GetCurrenciesAsync(cancellationToken);

                return Ok(currencies);
            }, cancellationToken);
        }

        [HttpGet("companies")]
        public Task<IActionResult> Companies(
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var companies = await _context.Companies
                    .AsNoTracking()
                    .OrderBy(row => row.Name)
                    .ToListAsync(cancellationToken);

                return Ok(companies);
            }, cancellationToken);
        }

        [HttpPost("companies")]
        public Task<IActionResult> AddCompany(
            [FromBody] CompanyDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var country = await _lookups.ResolveCountryAsync(
                    dto.Country,
                    cancellationToken);

                var company = new Company
                {
                    Name = dto.Name.Trim(),
                    Country = dto.Country,
                    Currency = country.CurrencyCode
                };

                _context.Companies.Add(company);

                await _context.SaveChangesAsync(
                    cancellationToken);

                return StatusCode(
                    StatusCodes.Status201Created,
                    company);
            }, cancellationToken);
        }

        [HttpGet("bank-accounts")]
        public Task<IActionResult> BankAccounts(
            CancellationToken cancellationToken,
            [FromQuery] int? companyId)
        {
            return HandleAsync(async () =>
            {
                var query = _context.BankAccounts
                    .AsNoTracking();

                if (companyId.HasValue)
                {
                    query = query.Where(row =>
                        row.CompanyId == companyId);
                }

                var accounts = await query
                    .OrderBy(row => row.Name)
                    .Select(row => new
                    {
                        row.Id,
                        row.CompanyId,
                        row.Name,
                        row.Currency,
                        row.LastFour
                    })
                    .ToListAsync(cancellationToken);

                return Ok(accounts);
            }, cancellationToken);
        }

        [HttpPost("bank-accounts")]
        public Task<IActionResult> AddAccount(
            [FromBody] BankAccountDto dto,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var companyExists = await _context.Companies
                    .AnyAsync(
                        row => row.Id == dto.CompanyId,
                        cancellationToken);

                if (!companyExists)
                {
                    throw new ValidationException(
                        "Select an existing company.");
                }

                var currency = await _lookups.ResolveCurrencyAsync(
                dto.Currency,
                true,
                cancellationToken);

                var account = new BankAccount
                {
                    CompanyId = dto.CompanyId,
                    Name = dto.Name.Trim(),
                    Currency = currency.CurrencyCode,
                    CurrencyDecimalPlaces = currency.DecimalPlaces,
                    LastFour = dto.LastFour ?? ""
                };

                _context.BankAccounts.Add(account);

                await _context.SaveChangesAsync(
                    cancellationToken);

                return StatusCode(
                    StatusCodes.Status201Created,
                    new
                    {
                        account.Id,
                        account.CompanyId,
                        account.Name,
                        account.Currency,
                        account.LastFour
                    });
            }, cancellationToken);
        }

        [HttpPost("{id:int}/receipt")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public Task<IActionResult> UploadReceipt(
            int id,
            IFormFile file,
            [FromForm] string rowVersion,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var expense = await _context.Expenses
                    .SingleOrDefaultAsync(
                        row => row.Id == id,
                        cancellationToken);

                if (expense == null)
                {
                    throw new KeyNotFoundException(
                        "Expense not found.");
                }

                if (expense.Status != "Draft")
                {
                    throw new InvalidOperationException(
                        Services.ExpenseService.LockedMessage);
                }

                byte[] version;

                try
                {
                    version = Convert.FromBase64String(
                        rowVersion);
                }
                catch (FormatException)
                {
                    throw new ValidationException(
                        "Invalid expense version. Refresh and try again.");
                }

                ExpenseHelper.CheckVersion(
                    expense.RowVersion,
                    version);

                if (file.Length == 0 || file.Length > 5 * 1024 * 1024)
                {
                    throw new ValidationException(
                        "Choose a receipt up to 5 MB.");
                }

                using var stream = new MemoryStream();

                await file.CopyToAsync(
                    stream,
                    cancellationToken);

                var bytes = stream.ToArray();
                var contentType = ReceiptType(bytes);

                if (contentType == null)
                {
                    throw new ValidationException(
                        "Choose a PDF, PNG, JPEG, or WebP receipt.");
                }

                var fileName = Path.GetFileName(
                    file.FileName.Replace('\\', '/'));

                if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 200)
                {
                    throw new ValidationException(
                        "Receipt filename must be between 1 and 200 characters.");
                }

                var receipt = await _context.Receipts
                    .SingleOrDefaultAsync(
                        row => row.ExpenseId == id,
                        cancellationToken);

                if (receipt == null)
                {
                    receipt = new Receipt
                    {
                        ExpenseId = id
                    };

                    _context.Receipts.Add(receipt);
                }

                receipt.FileName = fileName;
                receipt.ContentType = contentType;
                receipt.Content = bytes;

                expense.ModifiedDate = DateTime.UtcNow;

                _context.Entry(expense)
                    .Property(row => row.RowVersion)
                    .OriginalValue = version;

                await _context.SaveChangesAsync(
                    cancellationToken);

                var result = await _service.GetByIdAsync(
                    id,
                    expense.Currency,
                    cancellationToken);

                return Ok(result);
            }, cancellationToken);
        }

        [HttpGet("{id:int}/receipt")]
        public Task<IActionResult> DownloadReceipt(
            int id,
            CancellationToken cancellationToken)
        {
            return HandleAsync(async () =>
            {
                var receipt = await _context.Receipts
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        row => row.ExpenseId == id,
                        cancellationToken);

                if (receipt == null)
                {
                    throw new KeyNotFoundException(
                        "Receipt not found.");
                }

                Response.Headers["X-Content-Type-Options"] = "nosniff";

                return File(
                    receipt.Content,
                    receipt.ContentType,
                    receipt.FileName);
            }, cancellationToken);
        }

        private static string? ReceiptType(
            byte[] bytes)
        {
            if (
                bytes.Length >= 5 &&
                bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            {
                return "application/pdf";
            }

            if (
                bytes.Length >= 8 &&
                bytes.AsSpan(0, 8).SequenceEqual(
                    new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            {
                return "image/png";
            }

            if (
                bytes.Length >= 3 &&
                bytes[0] == 255 &&
                bytes[1] == 216 &&
                bytes[2] == 255)
            {
                return "image/jpeg";
            }

            if (
                bytes.Length >= 12 &&
                bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            {
                return "image/webp";
            }

            return null;
        }

        private async Task<IActionResult> HandleAsync(
            Func<Task<IActionResult>> action,
            CancellationToken cancellationToken)
        {
            try
            {
                return await action();
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (SqlException exception)
                when (cancellationToken.IsCancellationRequested &&
                      exception.Number == 0)
            {
                return StatusCode(499);
            }
            catch (InvalidOperationException exception)
                when (cancellationToken.IsCancellationRequested &&
                      exception.Message == "Operation cancelled by user.")
            {
                return StatusCode(499);
            }
            catch (OperationCanceledException)
            {
                return StatusCode(504, new
                {
                    message = "Exchange-rate lookup timed out. Retry shortly."
                });
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(exception, "Automatic exchange-rate lookup failed.");

                return StatusCode(503, new
                {
                    message = "Unable to load an automatic exchange rate. Retry shortly."
                });
            }
            catch (ValidationException exception)
            {
                return BadRequest(new
                {
                    message = exception.Message
                });
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new
                {
                    message = exception.Message
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message = "This expense changed. Refresh before trying again."
                });
            }
            catch (InvalidOperationException exception)
                when (exception.Message == Services.ExpenseService.LockedMessage)
            {
                return Conflict(new
                {
                    message = exception.Message
                });
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                return Conflict(new
                {
                    message = "This name or request already exists. Refresh before trying again."
                });
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Unable to save expense changes.");

                return Problem(
                    statusCode: 500,
                    title: "Unable to save expense changes.");
            }
            catch (SqlException exception)
            {
                _logger.LogError(
                    exception,
                    "Expense database unavailable.");

                return Problem(
                    statusCode: 503,
                    title: "Expense database unavailable. Check the connection and apply the migration.");
            }
        }
    }
}

