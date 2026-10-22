using System.ComponentModel.DataAnnotations;
using BankingService.API.DTOs;
using BankingService.API.Models;
using BankingService.API.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Services
{
    public class BankTransactionService : IBankTransactionService
    {
        public const string RequestConflictMessage =
            "This request ID was already used for different transaction details.";

        private readonly BankingService.API.Data.BankingDbContext _context;
        private readonly IBankTransactionRepository _repository;
        private readonly IBankAccountRepository _accounts;

        public BankTransactionService(
            BankingService.API.Data.BankingDbContext context,
            IBankTransactionRepository repository,
            IBankAccountRepository accounts)
        {
            _context = context;
            _repository = repository;
            _accounts = accounts;
        }

        public async Task<BankTransactionListDto> GetPagedAsync(
            BankTransactionQueryDto query,
            CancellationToken cancellationToken)
        {
            BankTransactionHelper.Validate(query);

            var source = _repository.Query();

            if (query.BankAccountId.HasValue)
            {
                source = source.Where(
                    transaction =>
                        transaction.BankAccountId == query.BankAccountId.Value);
            }

            if (query.CompanyId.HasValue)
            {
                source = source.Where(
                    transaction =>
                        transaction.CompanyId == query.CompanyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Currency))
            {
                var currency = query.Currency.Trim().ToUpperInvariant();

                source = source.Where(
                    transaction => transaction.Currency == currency);
            }

            if (query.Direction.HasValue)
            {
                source = source.Where(
                    transaction =>
                        transaction.Direction == query.Direction.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                source = source.Where(
                    transaction => transaction.Status == query.Status);
            }

            if (query.From.HasValue)
            {
                source = source.Where(
                    transaction =>
                        transaction.TransactionDate >= query.From.Value);
            }

            if (query.To.HasValue)
            {
                source = source.Where(
                    transaction =>
                        transaction.TransactionDate <= query.To.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();

                source = source.Where(
                    transaction =>
                        transaction.Reference.Contains(search) ||
                        transaction.Description.Contains(search) ||
                        transaction.BankAccount.Name.Contains(search));
            }

            var total = await source.CountAsync(
                cancellationToken);

            var lastPage = Math.Max(
                1,
                (int)Math.Ceiling(total / (double)query.PageSize));

            var page = Math.Min(
                query.Page,
                lastPage);

            var transactions = await source
                .Include(transaction => transaction.BankAccount)
                .OrderByDescending(transaction => transaction.TransactionDate)
                .ThenByDescending(transaction => transaction.Id)
                .Skip((page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            var companies = await _accounts.GetCompaniesAsync(
                cancellationToken);

            var companyNames = companies.ToDictionary(
                company => company.Id,
                company => company.Name);

            var data = transactions.Select(transaction =>
            {
                companyNames.TryGetValue(
                    transaction.CompanyId,
                    out var companyName);

                return ToResponse(
                    transaction,
                    companyName ?? "");
            }).ToList();

            return new BankTransactionListDto
            {
                Data = data,
                Total = total,
                Page = page,
                PageSize = query.PageSize,
            };
        }

        public async Task<BankTransactionResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var transaction = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (transaction == null)
            {
                return null;
            }

            return await ResponseAsync(
                transaction,
                cancellationToken);
        }

        public async Task<BankTransactionResponseDto> CreateAsync(
            CreateBankTransactionDto dto,
            CancellationToken cancellationToken)
        {
            BankTransactionHelper.Validate(dto);

            var fingerprint = BankTransactionHelper.CreateFingerprint(dto);

            var existing = await _repository.GetByRequestIdAsync(
                dto.RequestId,
                cancellationToken);

            if (existing != null)
            {
                CheckFingerprint(
                    existing,
                    fingerprint);

                return await ResponseAsync(
                    existing,
                    cancellationToken);
            }

            var account = await _accounts.GetByIdAsync(
                dto.BankAccountId,
                cancellationToken);

            if (account == null)
            {
                throw new ValidationException(
                    "Select an existing bank account.");
            }

            BankTransactionHelper.ValidateAccount(
                account,
                dto.TransactionDate,
                dto.Amount);

            var transaction = new BankTransaction
            {
                RequestId = dto.RequestId,
                RequestFingerprint = fingerprint,
                BankAccountId = account.Id,
                BankAccount = account,
                CompanyId = account.CompanyId,
                TransactionDate = dto.TransactionDate,
                Direction = dto.Direction,
                Amount = dto.Amount,
                Currency = account.Currency,
                CurrencyDecimalPlaces = account.CurrencyDecimalPlaces,
                Status = "Draft",
                Reference = dto.Reference?.Trim() ?? "",
                Description = dto.Description?.Trim() ?? "",
                Notes = dto.Notes?.Trim() ?? "",
                CreatedDate = DateTime.UtcNow,
            };

            await _repository.AddAsync(
                transaction,
                cancellationToken);

            try
            {
                await _repository.SaveChangesAsync(
                    cancellationToken);
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is SqlException
                {
                    Number: 2601 or 2627
                })
            {
                var duplicate = await _repository.Query()
                    .Include(row => row.BankAccount)
                    .SingleOrDefaultAsync(
                        row => row.RequestId == dto.RequestId,
                        cancellationToken);

                if (duplicate == null)
                {
                    throw;
                }

                CheckFingerprint(
                    duplicate,
                    fingerprint);

                return await ResponseAsync(
                    duplicate,
                    cancellationToken);
            }

            return await ResponseAsync(
                transaction,
                cancellationToken);
        }

        public async Task<BankTransactionResponseDto?> UpdateAsync(
            int id,
            UpdateBankTransactionDto dto,
            CancellationToken cancellationToken)
        {
            BankTransactionHelper.Validate(dto);

            var transaction = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (transaction == null)
            {
                return null;
            }

            BankTransactionHelper.CheckVersion(
                transaction.RowVersion,
                dto.RowVersion);

            BankTransactionHelper.CheckDraft(transaction);

            CheckAccountSnapshot(transaction);

            BankTransactionHelper.ValidateAccount(
                transaction.BankAccount,
                dto.TransactionDate,
                dto.Amount);

            transaction.TransactionDate = dto.TransactionDate;
            transaction.Direction = dto.Direction;
            transaction.Amount = dto.Amount;
            transaction.Reference = dto.Reference?.Trim() ?? "";
            transaction.Description = dto.Description?.Trim() ?? "";
            transaction.Notes = dto.Notes?.Trim() ?? "";
            transaction.ModifiedDate = DateTime.UtcNow;

            await _repository.SaveChangesAsync(
                cancellationToken);

            return await ResponseAsync(
                transaction,
                cancellationToken);
        }

        public async Task<BankTransactionResponseDto?> PostAsync(
            int id,
            BankTransactionVersionDto dto,
            CancellationToken cancellationToken)
        {
            BankTransactionHelper.Validate(dto);

            await using var databaseTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var transaction = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (transaction == null)
            {
                return null;
            }

            BankTransactionHelper.CheckVersion(
                transaction.RowVersion,
                dto.RowVersion);

            if (transaction.Status == "Posted")
            {
                return await ResponseAsync(
                    transaction,
                    cancellationToken);
            }

            BankTransactionHelper.CheckDraft(transaction);

            CheckAccountSnapshot(transaction);

            BankTransactionHelper.ValidateAccount(
                transaction.BankAccount,
                transaction.TransactionDate,
                transaction.Amount);

            await BankingPaymentLock.AcquireAsync(_context, "Account." + transaction.BankAccountId, cancellationToken);
            if (await _context.PaymentBankingLinks.AnyAsync(row =>
                    row.BankTransactionId == id || row.ReversalTransactionId == id, cancellationToken) &&
                await _context.BankReconciliations.AnyAsync(row => row.BankAccountId == transaction.BankAccountId &&
                    row.Status == "Completed" && row.PeriodEnd >= transaction.TransactionDate, cancellationToken))
            {
                throw new ValidationException("This automatic transaction is dated in a completed reconciliation. Review its actual bank movement date before posting.");
            }

            var now = DateTime.UtcNow;

            transaction.Status = "Posted";
            transaction.PostedDate = now;
            transaction.ModifiedDate = now;

            await _repository.SaveChangesAsync(
                cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return await ResponseAsync(
                transaction,
                cancellationToken);
        }

        public async Task<BankTransactionResponseDto?> CancelAsync(
            int id,
            BankTransactionVersionDto dto,
            CancellationToken cancellationToken)
        {
            BankTransactionHelper.Validate(dto);

            var transaction = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (transaction == null)
            {
                return null;
            }

            BankTransactionHelper.CheckVersion(
                transaction.RowVersion,
                dto.RowVersion);

            if (transaction.Status == "Cancelled")
            {
                return await ResponseAsync(
                    transaction,
                    cancellationToken);
            }

            BankTransactionHelper.CheckDraft(transaction);

            var now = DateTime.UtcNow;

            transaction.Status = "Cancelled";
            transaction.CancelledDate = now;
            transaction.ModifiedDate = now;

            await _repository.SaveChangesAsync(
                cancellationToken);

            return await ResponseAsync(
                transaction,
                cancellationToken);
        }

        private async Task<BankTransactionResponseDto> ResponseAsync(
            BankTransaction transaction,
            CancellationToken cancellationToken)
        {
            var company = await _accounts.GetCompanyAsync(
                transaction.CompanyId,
                cancellationToken);

            return ToResponse(
                transaction,
                company?.Name ?? "");
        }

        private static void CheckFingerprint(
            BankTransaction transaction,
            string fingerprint)
        {
            if (transaction.RequestFingerprint != fingerprint)
            {
                throw new InvalidOperationException(
                    RequestConflictMessage);
            }
        }

        private static void CheckAccountSnapshot(
            BankTransaction transaction)
        {
            var account = transaction.BankAccount;

            if (
                transaction.CompanyId != account.CompanyId ||
                transaction.Currency != account.Currency ||
                transaction.CurrencyDecimalPlaces != account.CurrencyDecimalPlaces)
            {
                throw new ValidationException(
                    "The transaction's company or currency details no longer match the account.");
            }
        }

        private static BankTransactionResponseDto ToResponse(
            BankTransaction transaction,
            string companyName)
        {
            return new BankTransactionResponseDto
            {
                Id = transaction.Id,
                RequestId = transaction.RequestId,
                BankAccountId = transaction.BankAccountId,
                BankAccountName = transaction.BankAccount.Name,
                CompanyId = transaction.CompanyId,
                CompanyName = companyName,
                TransactionDate = transaction.TransactionDate,
                Direction = transaction.Direction,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                CurrencyDecimalPlaces = transaction.CurrencyDecimalPlaces,
                Status = transaction.Status,
                Reference = transaction.Reference,
                Description = transaction.Description,
                Notes = transaction.Notes,
                ReversalOfTransactionId = transaction.ReversalOfTransactionId,
                CreatedDate = transaction.CreatedDate,
                ModifiedDate = transaction.ModifiedDate,
                PostedDate = transaction.PostedDate,
                CancelledDate = transaction.CancelledDate,
                RowVersion = transaction.RowVersion,
            };
        }
    }
}