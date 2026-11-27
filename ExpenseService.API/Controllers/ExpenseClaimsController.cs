using SparkFinance.Http;
using System.ComponentModel.DataAnnotations;
using ExpenseService.API.Data;
using ExpenseService.API.DTOs;
using ExpenseService.API.Models;
using ExpenseService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SparkFinance.Database;

namespace ExpenseService.API.Controllers
{
    [ApiController]
    [Route("api/expense-claims")]
    [ServiceFilter(typeof(ExpenseClaimAccessFilter))]
    public class ExpenseClaimsController : ControllerBase
    {
        private readonly ExpenseDbContext _context;
        private readonly IExpenseLookupService _lookups;
        private readonly IExpenseExchangeRateService _rates;
        private readonly IWebHostEnvironment _environment;
        public ExpenseClaimsController(
            ExpenseDbContext context,
            IExpenseLookupService lookups,
            IExpenseExchangeRateService rates,
            IWebHostEnvironment environment
        )
        {
            _context = context;
            _lookups = lookups;
            _rates = rates;
            _environment = environment;
        }

        private FinanceCompanyScope Scope =>
            (FinanceCompanyScope)HttpContext.Items[typeof(ExpenseClaimAccessFilter)]!;

        private (string Id, bool Admin) Actor()
        {
            if (string.IsNullOrWhiteSpace(Scope.ActorId))
            {
                throw new UnauthorizedAccessException("Sign in to manage expense claims.");
            }
            return (Scope.ActorId, Scope.IsAdmin);
        }

        private async Task<IActionResult> Handle(Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
            }
            catch (UnauthorizedAccessException exception)
            {
                return Unauthorized(new
                {
                    message = exception.Message
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
            catch (InvalidOperationException exception)
            {
                return Conflict(new
                {
                    message = exception.Message
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    message = "The claim changed. Refresh and try again."
                });
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && sql.Number is 2601 or 2627)
            {
                return Conflict(new
                {
                    message = "This claim reference or request already exists. Refresh before retrying."
                });
            }
            catch (Exception cancellationException)
                when (RequestCancellation.IsCancellation(
                    cancellationException,
                    HttpContext?.RequestAborted ?? CancellationToken.None))
            {
                return StatusCode(499);
            }
            catch (SqlException exception) when (exception.Number == 208)
            {
                return Problem(
                    statusCode: 503,
                    title: "Expense Claims database setup is pending. Apply the claim migration first."
                );
            }
        }

        [HttpGet("lookups/choices")]
        public Task<IActionResult> Choices(
            [FromServices] CompanySetupService setup,
            CancellationToken cancellationToken)
        {
            return Handle(async () =>
            {
                var companies = await setup.GetAllAsync(cancellationToken);
                var currencies = await _lookups.GetCurrenciesAsync(cancellationToken);
                return Ok(new
                {
                    branches = companies.Where(company => Scope.AllowsCompany(company.CompanyId))
                        .Select(company => new
                        {
                            name = company.BranchName,
                            accountingCompanyId = company.CompanyId,
                            currency = company.Currency
                        }),
                    currencies = currencies.Where(currency => currency.IsActive)
                        .Select(currency => currency.CurrencyCode)
                });
            });
        }

        [HttpGet("lookups/accounts")]
        public Task<IActionResult> Accounts(
            [FromQuery, Range(1, int.MaxValue)] int companyId,
            CancellationToken cancellationToken)
        {
            return Handle(async () =>
            {
                var table = _context.Database.IsFinanceMySql() ? "accounting__Accounts" : "accounting.Accounts";
                var sql = $$"""
                    SELECT Id, Code, Name, Type FROM {{table}}
                    WHERE CompanyId = {0} AND IsActive = 1 AND IsPostingAccount = 1
                    ORDER BY Code
                    """;
                return Ok(await _context.Database.SqlQueryRaw<ClaimAccountLookup>(sql, companyId)
                    .ToListAsync(cancellationToken));
            });
        }

        public sealed class ClaimAccountLookup
        {
            public int Id { get; set; }
            public string Code { get; set; } = "";
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
        }

        [HttpGet]
        public Task<IActionResult> List(CancellationToken cancellationToken) => Handle(async () =>
        {
            var query = _context.ExpenseClaims.AsNoTracking();
            if (Scope.CompanyIds != null)
            {
                query = query.Where(claim => Scope.CompanyIds.Contains(claim.CompanyId));
            }
            var rows = await query.Include(row => row.Receipts).Include(row => row.History).AsSplitQuery().OrderByDescending(row => row.Id).ToListAsync(cancellationToken);
            return Ok(rows);
        });
        private static void CheckVersion(
            ExpenseClaim claim,
            string version
        )
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(version);
            }
            catch (FormatException)
            {
                throw new ValidationException("Refresh the claim before saving.");
            }

            if (!claim.RowVersion.SequenceEqual(bytes))
            {
                throw new InvalidOperationException("The claim changed. Refresh and try again.");
            }
        }

        private async Task<ExpenseClaim> Find(
            int id,
            CancellationToken cancellationToken
        ) => await _context.ExpenseClaims.Include(row => row.Receipts).Include(row => row.History).AsSplitQuery().SingleOrDefaultAsync(row => row.Id == id, cancellationToken) ?? throw new KeyNotFoundException("Claim not found.");
        [HttpPost]
        public Task<IActionResult> Create(
            ExpenseClaimDto dto,
            CancellationToken cancellationToken
        ) => Save(null, dto, cancellationToken);
        [HttpPut("{id:int}")]
        public Task<IActionResult> Update(
            int id,
            ExpenseClaimDto dto,
            CancellationToken cancellationToken
        ) => Save(id, dto, cancellationToken);
        private Task<IActionResult> Save(
            int? id,
            ExpenseClaimDto dto,
            CancellationToken cancellationToken
        ) => Handle(async () =>
        {
            var actor = Actor();
            if (dto.RequestId == Guid.Empty || dto.Date == default || dto.Date > DateOnly.FromDateTime(DateTime.Today))
            {
                throw new ValidationException("A request ID and a valid claim date are required.");
            }

            if (dto.Amount != decimal.Round(dto.Amount, 2))
            {
                throw new ValidationException("Use at most two decimal places for a claim amount.");
            }

            if (new[]
                        {
                dto.Reference,
                dto.Employee,
                dto.Category,
                dto.Purpose,
                dto.Branch,
                dto.Currency
            }.Any(string.IsNullOrWhiteSpace))
            {
                throw new ValidationException("Complete all required claim fields.");
            }

            var company = await _context.Companies.AsNoTracking().SingleOrDefaultAsync(row => row.Id == dto.CompanyId, cancellationToken) ?? throw new ValidationException("Select an existing company.");
            var branchesTable = _context.Database.IsFinanceMySql() ? "amnikon__Branches" : "amnikon.Branches";
            var branchSql = $"SELECT COUNT(*) AS Value FROM {branchesTable} WHERE Name = {{0}} AND IsActive = 1 AND AccountingCompanyId = {{1}}";
            var branchMatches = (await _context.Database.SqlQueryRaw<int>(branchSql,
                dto.Branch, dto.CompanyId).ToListAsync(cancellationToken)).Single();
            if (branchMatches != 1)
            {
                throw new ValidationException("Select a branch linked to this accounting company.");
            }

            await _lookups.ResolveCurrencyAsync(dto.Currency, true, cancellationToken);
            var existing = !id.HasValue ? await _context.ExpenseClaims.AsNoTracking().SingleOrDefaultAsync(row => row.RequestId == dto.RequestId, cancellationToken) : null;
            if (existing != null)
            {
                if (!ExpenseClaimWorkflow.IsOwner(existing.CreatedBy, actor.Id))
                {
                    throw new UnauthorizedAccessException("This request belongs to a different operator.");
                }

                if (existing.CompanyId != dto.CompanyId || existing.Reference != dto.Reference.Trim() || existing.Amount != dto.Amount || existing.Currency != dto.Currency.Trim().ToUpperInvariant() || existing.Date != dto.Date || existing.Employee != dto.Employee.Trim() || existing.Branch != dto.Branch.Trim() || existing.Purpose != dto.Purpose.Trim() || existing.Category != dto.Category.Trim() || existing.Notes != dto.Notes.Trim())
                {
                    throw new InvalidOperationException("This request ID was already used for different claim details. Refresh before retrying.");
                }

                return Ok(existing);
            }

            var claim = id.HasValue ? await Find(id.Value, cancellationToken) : new ExpenseClaim
            {
                CreatedBy = actor.Id,
                RequestId = dto.RequestId
            };
            if (claim.Status != "Draft" || (!actor.Admin && !ExpenseClaimWorkflow.IsOwner(claim.CreatedBy, actor.Id)))
            {
                throw new InvalidOperationException("Only the owner or administrator can edit a draft.");
            }

            if (id.HasValue)
            {
                CheckVersion(claim, dto.RowVersion);
            }

            claim.CompanyId = dto.CompanyId;
            claim.Branch = dto.Branch.Trim();
            claim.Currency = dto.Currency.Trim().ToUpperInvariant();
            claim.Date = dto.Date;
            claim.Reference = dto.Reference.Trim();
            claim.Employee = dto.Employee.Trim();
            claim.Amount = dto.Amount;
            claim.Category = dto.Category.Trim();
            claim.Purpose = dto.Purpose.Trim();
            claim.Notes = dto.Notes.Trim();
            claim.ReportingCurrency = company.Currency;
            claim.ModifiedDate = DateTime.UtcNow;
            claim.History.Add(new ExpenseClaimHistory { Actor = actor.Id, Status = "Draft", Reason = id.HasValue ? "Draft updated" : "Draft created" });
            if (!id.HasValue)
            {
                _context.ExpenseClaims.Add(claim);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Ok(claim);
        });
        [HttpPatch("{id:int}/status")]
        public Task<IActionResult> ChangeStatus(
            int id,
            ExpenseClaimActionDto dto,
            CancellationToken cancellationToken
        ) => Handle(async () =>
        {
            var actor = Actor();
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
            var claim = await Find(id, cancellationToken);
            CheckVersion(claim, dto.RowVersion);
            var permittedDecision = dto.Status switch
            {
                "Approved" or "Rejected" => Scope.AllowsCompanyAction(claim.CompanyId, Scope.ApprovableCompanyIds),
                "Paid" => Scope.AllowsCompanyAction(claim.CompanyId, Scope.PostableCompanyIds),
                _ => actor.Admin
            };
            ExpenseClaimWorkflow.Validate(claim.Status, dto.Status, claim.CreatedBy, actor.Id, permittedDecision, dto.Reason);
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (dto.Status == "Approved")
            {
                var date = dto.ApprovedDate ?? today;
                if (date < claim.Date || date > today)
                {
                    throw new ValidationException("Approval date must be between the claim date and today.");
                }

                var rate = await _rates.GetRateAsync(claim.Currency, claim.ReportingCurrency, claim.Date, cancellationToken);
                claim.ExchangeRate = rate.Rate;
                claim.RateDate = rate.RateDate;
                claim.ExpenseAccountId = dto.ExpenseAccountId;
                claim.PayableAccountId = dto.PayableAccountId;
                await ValidateAccount(claim.CompanyId, dto.ExpenseAccountId, "Expense", cancellationToken);
                await ValidateAccount(claim.CompanyId, dto.PayableAccountId, "Liability", cancellationToken);
                claim.ApprovedBy = actor.Id;
                claim.ApprovedDate = date;
                await Post(
                    claim,
                    date,
                    dto.ExpenseAccountId!.Value,
                    dto.PayableAccountId!.Value,
                    "approval",
                    cancellationToken
                );
            }

            if (dto.Status == "Paid")
            {
                if (!dto.PaidDate.HasValue || dto.PaidDate < claim.ApprovedDate || dto.PaidDate > today || string.IsNullOrWhiteSpace(dto.PaymentReference))
                {
                    throw new ValidationException("Enter a payment date on or after approval, and a payment reference.");
                }

                await ValidateAccount(claim.CompanyId, dto.PaymentAccountId, "Asset", cancellationToken);
                claim.PaymentAccountId = dto.PaymentAccountId;
                claim.PaidDate = dto.PaidDate;
                claim.PaymentReference = dto.PaymentReference.Trim();
                claim.PaidBy = actor.Id;
                await Post(
                    claim,
                    dto.PaidDate.Value,
                    claim.PayableAccountId!.Value,
                    dto.PaymentAccountId!.Value,
                    "payment",
                    cancellationToken
                );
            }

            claim.Status = dto.Status;
            claim.DecisionReason = dto.Status == "Rejected" ? dto.Reason.Trim() : claim.DecisionReason;
            claim.ModifiedDate = DateTime.UtcNow;
            claim.History.Add(new ExpenseClaimHistory { Actor = actor.Id, Status = dto.Status, Reason = dto.Reason.Trim() });
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(claim);
        });
        private async Task ValidateAccount(
            int companyId,
            int? id,
            string type,
            CancellationToken cancellationToken
        )
        {
            var accountsTable = _context.Database.IsFinanceMySql() ? "accounting__Accounts" : "accounting.Accounts";
            var accountsSql = $"SELECT COUNT(*) AS Value FROM {accountsTable} WHERE Id = {{0}} AND CompanyId = {{1}} AND Type = {{2}} AND IsActive = 1 AND IsPostingAccount = 1";
            var count = (await _context.Database.SqlQueryRaw<int>(accountsSql,
                id!, companyId, type).ToListAsync(cancellationToken)).Single();
            if (count != 1)
            {
                throw new ValidationException($"Select an active {type.ToLowerInvariant()} posting account for this company.");
            }
        }

        private async Task Post(
            ExpenseClaim claim,
            DateOnly date,
            int debit,
            int credit,
            string stage,
            CancellationToken cancellationToken
        )
        {
            // Same database transaction as the status change: both the claim and ledger commit, or neither does.
            var documentId = $"Claim:{claim.Id}:{stage}";
            var entryNumber = $"CLM-{claim.Id}-{stage}";
            var narration = $"Expense claim {claim.Reference} · {stage} · {claim.Employee}";
            var total = claim.Amount;
            var baseAmount = decimal.Round(total * claim.ExchangeRate, 2, MidpointRounding.AwayFromZero);
            if (baseAmount <= 0)
            {
                throw new ValidationException("The converted claim amount is too small to post in the company currency.");
            }

            var now = DateTime.UtcNow;
            if (_context.Database.IsFinanceMySql())
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO accounting__JournalEntries
                        (CompanyId, EntryNumber, EntryDate, Branch, Currency, ExchangeRate, Narration, Reference,
                         Source, SourceDocumentId, SourceDocumentNumber, Status, TotalDebit, TotalCredit, CreatedDate, PostedDate)
                    VALUES ({claim.CompanyId}, {entryNumber}, {date}, {claim.Branch}, {claim.Currency}, {claim.ExchangeRate},
                        {narration}, {claim.Reference}, 'Expense', {documentId}, {claim.Reference}, 'Posted', {total}, {total}, {now}, {now})
                    """, cancellationToken);
                var entryId = (await _context.Database.SqlQueryRaw<int>("SELECT LAST_INSERT_ID() AS Value")
                    .ToListAsync(cancellationToken)).Single();
                await _context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO accounting__JournalEntryLines
                        (JournalEntryId, CompanyId, LineNumber, AccountId, Description, DebitAmount, CreditAmount, BaseDebitAmount, BaseCreditAmount)
                    VALUES ({entryId}, {claim.CompanyId}, 1, {debit}, {narration}, {total}, {0m}, {baseAmount}, {0m}),
                           ({entryId}, {claim.CompanyId}, 2, {credit}, {narration}, {0m}, {total}, {0m}, {baseAmount})
                    """, cancellationToken);
            }
            else
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"
                INSERT INTO accounting.JournalEntries
                    (CompanyId, EntryNumber, EntryDate, Branch, Currency, ExchangeRate, Narration, Reference,
                     Source, SourceDocumentId, SourceDocumentNumber, Status, TotalDebit, TotalCredit, CreatedDate, PostedDate)
                VALUES ({claim.CompanyId}, {entryNumber}, {date}, {claim.Branch}, {claim.Currency}, {claim.ExchangeRate},
                    {narration}, {claim.Reference}, N'Expense', {documentId}, {claim.Reference}, N'Posted', {total}, {total}, {now}, {now});
                DECLARE @entry int = CONVERT(int, SCOPE_IDENTITY());
                INSERT INTO accounting.JournalEntryLines
                    (JournalEntryId, CompanyId, LineNumber, AccountId, Description, DebitAmount, CreditAmount, BaseDebitAmount, BaseCreditAmount)
                VALUES (@entry, {claim.CompanyId}, 1, {debit}, {narration}, {total}, 0, {baseAmount}, 0),
                       (@entry, {claim.CompanyId}, 2, {credit}, {narration}, 0, {total}, 0, {baseAmount});",
                cancellationToken
                );
            }
            await SparkFinance.Auditing.FinanceAudit.WriteAsync(
                _context,
                "ClaimJournal",
                documentId,
                "Created",
                null,
                new Dictionary<string, object?> { ["CompanyId"] = claim.CompanyId, ["Branch"] = claim.Branch, ["Reference"] = claim.Reference, ["EntryDate"] = date, ["Amount"] = total, ["Currency"] = claim.Currency, ["ExchangeRate"] = claim.ExchangeRate, ["DebitAccountId"] = debit, ["CreditAccountId"] = credit, ["Status"] = "Posted" },
                cancellationToken
            );
        }

        [HttpPost("{id:int}/receipts")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public Task<IActionResult> Upload(
            int id,
            IFormFile file,
            [FromForm] string rowVersion,
            CancellationToken cancellationToken
        ) => Handle(async () =>
        {
            var actor = Actor();
            var claim = await Find(id, cancellationToken);
            CheckVersion(claim, rowVersion);
            if (claim.Status != "Draft" || (!actor.Admin && !ExpenseClaimWorkflow.IsOwner(claim.CreatedBy, actor.Id)))
            {
                throw new InvalidOperationException("Only editable drafts can receive attachments.");
            }

            if (claim.Receipts.Count >= 10 || file.Length <= 0 || file.Length > 5 * 1024 * 1024)
            {
                throw new ValidationException("Upload up to ten receipts, each no larger than 5 MB.");
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            var bytes = stream.ToArray();
            var type = bytes.AsSpan().StartsWith("%PDF-"u8) ? "application/pdf" : bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png" : bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 }) ? "image/jpeg" : null;
            if (type == null)
            {
                throw new ValidationException("Choose a PDF, PNG or JPEG receipt.");
            }

            claim.Receipts.Add(new ExpenseClaimReceipt { FileName = Path.GetFileName(file.FileName)[..Math.Min(200, Path.GetFileName(file.FileName).Length)], ContentType = type, Content = bytes, Size = file.Length });
            claim.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(claim);
        });
        [HttpGet("{id:int}/receipts/{receiptId:int}")]
        public Task<IActionResult> Download(
            int id,
            int receiptId,
            CancellationToken cancellationToken
        ) => Handle(async () =>
        {
            var actor = Actor();
            var claim = await Find(id, cancellationToken);
            var receipt = claim.Receipts.SingleOrDefault(row => row.Id == receiptId) ?? throw new KeyNotFoundException("Receipt not found.");
            return File(receipt.Content, receipt.ContentType, receipt.FileName);
        });
    }
}
