using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
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
    [Route("api/expense-claims")]
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

        private (string Id, bool Admin) Actor()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
                if (!string.IsNullOrWhiteSpace(id))
                    return (id, User.IsInRole("Admin"));
            }

            // Explicit local operator mode only. This is not authentication.
            // Production will require a verified identity when the auth module is connected.
            var local = Request.Headers["X-Claim-Operator"].ToString().Trim();
            if (_environment.IsDevelopment() && local.Length is> 0 and <= 200)
                return (local, Request.Headers["X-Claim-Role"] == "Admin");
            throw new UnauthorizedAccessException("Sign in first, or select a local operator in Development mode.");
        }

        private async Task<IActionResult> Handle(Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
            }
            catch (UnauthorizedAccessException exception)
            {
                return Unauthorized(new { message = exception.Message });
            }
            catch (ValidationException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new { message = exception.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "The claim changed. Refresh and try again." });
            }
            catch (DbUpdateException exception)when (exception.InnerException is SqlException sql && sql.Number is 2601 or 2627)
            {
                return Conflict(new { message = "This claim reference or request already exists. Refresh before retrying." });
            }
            catch (SqlException exception)when (exception.Number == 208)
            {
                return Problem(
                    statusCode: 503,
                    title: "Expense Claims database setup is pending. Apply the claim migration first."
                );
            }
        }

        [HttpGet]
        public Task<IActionResult> List(CancellationToken cancellationToken) => Handle(async () =>
        {
            var actor = Actor();
            var rows = await _context.ExpenseClaims.AsNoTracking().Where(row => actor.Admin || row.CreatedBy == actor.Id).Include(row => row.Receipts).Include(row => row.History).AsSplitQuery().OrderByDescending(row => row.Id).ToListAsync(cancellationToken);
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
                throw new InvalidOperationException("The claim changed. Refresh and try again.");
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
                throw new ValidationException("A request ID and a valid claim date are required.");
            if (dto.Amount != decimal.Round(dto.Amount, 2))
                throw new ValidationException("Use at most two decimal places for a claim amount.");
            if (new[]
            {
                dto.Reference,
                dto.Employee,
                dto.Category,
                dto.Purpose,
                dto.Branch,
                dto.Currency
            }.Any(string.IsNullOrWhiteSpace))
                throw new ValidationException("Complete all required claim fields.");
            var company = await _context.Companies.AsNoTracking().SingleOrDefaultAsync(row => row.Id == dto.CompanyId, cancellationToken) ?? throw new ValidationException("Select an existing company.");
            var branchMatches = await _context.Database.SqlQuery<int>($@"SELECT COUNT(*) AS Value FROM sales.BranchMasters
                WHERE Name = {dto.Branch} AND IsActive = 1 AND AccountingCompanyId = {dto.CompanyId}").SingleAsync(cancellationToken);
            if (branchMatches != 1)
                throw new ValidationException("Select a branch linked to this accounting company.");
            await _lookups.ResolveCurrencyAsync(dto.Currency, true, cancellationToken);
            var existing = !id.HasValue ? await _context.ExpenseClaims.AsNoTracking().SingleOrDefaultAsync(row => row.RequestId == dto.RequestId, cancellationToken) : null;
            if (existing != null)
            {
                if (!ExpenseClaimWorkflow.IsOwner(existing.CreatedBy, actor.Id))
                    throw new UnauthorizedAccessException("This request belongs to a different operator.");
                if (existing.CompanyId != dto.CompanyId || existing.Reference != dto.Reference.Trim() || existing.Amount != dto.Amount || existing.Currency != dto.Currency.Trim().ToUpperInvariant() || existing.Date != dto.Date || existing.Employee != dto.Employee.Trim() || existing.Branch != dto.Branch.Trim() || existing.Purpose != dto.Purpose.Trim() || existing.Category != dto.Category.Trim() || existing.Notes != dto.Notes.Trim())
                    throw new InvalidOperationException("This request ID was already used for different claim details. Refresh before retrying.");
                return Ok(existing);
            }

            var claim = id.HasValue ? await Find(id.Value, cancellationToken) : new ExpenseClaim
            {
                CreatedBy = actor.Id,
                RequestId = dto.RequestId
            };
            if (claim.Status != "Draft" || (!actor.Admin && !ExpenseClaimWorkflow.IsOwner(claim.CreatedBy, actor.Id)))
                throw new InvalidOperationException("Only the owner or administrator can edit a draft.");
            if (id.HasValue)
                CheckVersion(claim, dto.RowVersion);
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
                _context.ExpenseClaims.Add(claim);
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
            ExpenseClaimWorkflow.Validate(claim.Status, dto.Status, claim.CreatedBy, actor.Id, actor.Admin, dto.Reason);
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (dto.Status == "Approved")
            {
                var date = dto.ApprovedDate ?? today;
                if (date < claim.Date || date > today)
                    throw new ValidationException("Approval date must be between the claim date and today.");
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
                    throw new ValidationException("Enter a payment date on or after approval, and a payment reference.");
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
            var count = await _context.Database.SqlQuery<int>($@"SELECT COUNT(*) AS Value FROM accounting.Accounts
                WHERE Id = {id} AND CompanyId = {companyId} AND Type = {type} AND IsActive = 1 AND IsPostingAccount = 1").SingleAsync(cancellationToken);
            if (count != 1)
                throw new ValidationException($"Select an active {type.ToLowerInvariant()} posting account for this company.");
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
                throw new ValidationException("The converted claim amount is too small to post in the company currency.");
            var now = DateTime.UtcNow;
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
                throw new InvalidOperationException("Only editable drafts can receive attachments.");
            if (claim.Receipts.Count >= 10 || file.Length <= 0 || file.Length > 5 * 1024 * 1024)
                throw new ValidationException("Upload up to ten receipts, each no larger than 5 MB.");
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);
            var bytes = stream.ToArray();
            var type = bytes.AsSpan().StartsWith("%PDF-"u8) ? "application/pdf" : bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png" : bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 }) ? "image/jpeg" : null;
            if (type == null)
                throw new ValidationException("Choose a PDF, PNG or JPEG receipt.");
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
            if (!actor.Admin && !ExpenseClaimWorkflow.IsOwner(claim.CreatedBy, actor.Id))
                throw new UnauthorizedAccessException("This claim belongs to a different operator.");
            var receipt = claim.Receipts.SingleOrDefault(row => row.Id == receiptId) ?? throw new KeyNotFoundException("Receipt not found.");
            return File(receipt.Content, receipt.ContentType, receipt.FileName);
        });
    }
}
