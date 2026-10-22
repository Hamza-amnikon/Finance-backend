using System.ComponentModel.DataAnnotations;
using BankingService.API.DTOs;
using BankingService.API.Models;
using BankingService.API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Services
{
    public class BankReconciliationService : IBankReconciliationService
    {
        private readonly BankingService.API.Data.BankingDbContext _context;
        private readonly IBankReconciliationRepository _repository;
        private readonly IBankAccountRepository _accounts;
        private readonly IBankTransactionRepository _transactions;

        public BankReconciliationService(
            BankingService.API.Data.BankingDbContext context,
            IBankReconciliationRepository repository,
            IBankAccountRepository accounts,
            IBankTransactionRepository transactions)
        {
            _context = context;
            _repository = repository;
            _accounts = accounts;
            _transactions = transactions;
        }

        public async Task<BankReconciliationListDto> GetPagedAsync(
            BankReconciliationQueryDto query,
            CancellationToken cancellationToken)
        {
            BankReconciliationHelper.Validate(query);

            var reconciliations = _repository.Query();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();

                reconciliations = reconciliations.Where(reconciliation =>
                    reconciliation.BankAccount.Name.Contains(search) ||
                    reconciliation.Notes.Contains(search));
            }

            if (query.BankAccountId.HasValue)
            {
                reconciliations = reconciliations.Where(reconciliation =>
                    reconciliation.BankAccountId ==
                        query.BankAccountId.Value);
            }

            if (query.CompanyId.HasValue)
            {
                reconciliations = reconciliations.Where(reconciliation =>
                    reconciliation.CompanyId == query.CompanyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                reconciliations = reconciliations.Where(reconciliation =>
                    reconciliation.Status == query.Status);
            }

            var total = await reconciliations.CountAsync(
                cancellationToken);

            var lastPage = Math.Max(
                1,
                (int)Math.Ceiling(total / (double)query.PageSize));

            var page = Math.Min(
                query.Page,
                lastPage);

            var records = await reconciliations
                .Include(reconciliation => reconciliation.BankAccount)
                .OrderByDescending(reconciliation => reconciliation.PeriodEnd)
                .ThenByDescending(reconciliation => reconciliation.Id)
                .Skip((page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            var companies = await _accounts.GetCompaniesAsync(
                cancellationToken);

            var names = companies.ToDictionary(
                company => company.Id,
                company => company.Name);

            return new BankReconciliationListDto
            {
                Data = records.Select(reconciliation =>
                    MapHeader(reconciliation, names)).ToList(),

                Total = total,
                Page = page,
                PageSize = query.PageSize
            };
        }

        public async Task<BankReconciliationResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var reconciliation = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (reconciliation == null)
            {
                return null;
            }

            return await ToResponseAsync(
                reconciliation,
                cancellationToken);
        }

        public async Task<BankReconciliationResponseDto> CreateAsync(
            CreateBankReconciliationDto dto,
            CancellationToken cancellationToken)
        {
            BankReconciliationHelper.Validate(dto);

            if (dto.Lines == null)
            {
                throw new ValidationException(
                    "Statement lines are required.");
            }

            foreach (var line in dto.Lines)
            {
                if (line == null)
                {
                    throw new ValidationException(
                        "Statement lines cannot contain null entries.");
                }

                BankReconciliationHelper.Validate(line);
            }

            var fingerprint =
                BankReconciliationHelper.CreateFingerprint(dto);

            await using var databaseTransaction =
                await _repository.BeginTransactionAsync(
                    cancellationToken);

            var existing = await _repository.GetByRequestIdAsync(
                dto.RequestId,
                cancellationToken);

            if (existing != null)
            {
                if (existing.RequestFingerprint != fingerprint)
                {
                    throw new InvalidOperationException(
                        "This request ID was already used for different statement details.");
                }

                var existingResponse = await ToResponseAsync(
                    existing,
                    cancellationToken);

                await databaseTransaction.CommitAsync(
                    cancellationToken);

                return existingResponse;
            }

            var account = await _accounts.GetByIdAsync(
                dto.BankAccountId,
                cancellationToken);

            if (account == null)
            {
                throw new ValidationException(
                    "Bank account not found.");
            }

            ValidateAccount(account);

            BankReconciliationHelper.ValidateStatement(
                dto,
                account);

            var hasDraft = await _repository.Query()
                .AnyAsync(
                    reconciliation =>
                        reconciliation.BankAccountId == account.Id &&
                        reconciliation.Status == "Draft",
                    cancellationToken);

            if (hasDraft)
            {
                throw new InvalidOperationException(
                    "Complete the existing draft reconciliation for this account first.");
            }

            await ValidateSequenceAsync(
                account,
                dto.PeriodStart,
                dto.StatementOpeningBalance,
                cancellationToken);

            var reconciliation = new BankReconciliation
            {
                RequestId = dto.RequestId,
                RequestFingerprint = fingerprint,
                BankAccountId = account.Id,
                BankAccount = account,
                CompanyId = account.CompanyId,
                Currency = account.Currency,
                CurrencyDecimalPlaces = account.CurrencyDecimalPlaces,
                PeriodStart = dto.PeriodStart,
                PeriodEnd = dto.PeriodEnd,
                StatementOpeningBalance = dto.StatementOpeningBalance,
                StatementClosingBalance = dto.StatementClosingBalance,
                Status = "Draft",
                Notes = dto.Notes?.Trim() ?? "",
                CreatedDate = DateTime.UtcNow
            };

            for (var index = 0; index < dto.Lines.Count; index++)
            {
                var line = dto.Lines[index];

                reconciliation.Lines.Add(new BankStatementLine
                {
                    LineNumber = index + 1,
                    TransactionDate = line.TransactionDate,
                    Direction = line.Direction,
                    Amount = line.Amount,
                    Reference = line.Reference?.Trim() ?? "",
                    Description = line.Description?.Trim() ?? ""
                });
            }

            await _repository.AddAsync(
                reconciliation,
                cancellationToken);

            await _repository.SaveChangesAsync(
                cancellationToken);

            var response = await ToResponseAsync(
                reconciliation,
                cancellationToken);

            await databaseTransaction.CommitAsync(
                cancellationToken);

            return response;
        }

        public async Task<BankReconciliationResponseDto?> MatchAsync(
            int id,
            int lineId,
            MatchBankStatementLineDto dto,
            CancellationToken cancellationToken)
        {
            BankReconciliationHelper.Validate(dto);

            await using var databaseTransaction =
                await _repository.BeginTransactionAsync(
                    cancellationToken);

            var reconciliation = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (reconciliation == null)
            {
                return null;
            }

            ValidateSnapshot(reconciliation);

            BankReconciliationHelper.CheckDraft(reconciliation);

            BankReconciliationHelper.CheckVersion(
                reconciliation.RowVersion,
                dto.ReconciliationRowVersion);

            var line = reconciliation.Lines.FirstOrDefault(
                item => item.Id == lineId);

            if (line == null)
            {
                throw new ValidationException(
                    "The statement line does not belong to this reconciliation.");
            }

            BankReconciliationHelper.CheckVersion(
                line.RowVersion,
                dto.LineRowVersion);

            if (dto.BankTransactionId.HasValue)
            {
                var transaction = await _transactions.GetByIdAsync(
                    dto.BankTransactionId.Value,
                    cancellationToken);

                if (transaction == null)
                {
                    throw new ValidationException(
                        "Bank transaction not found.");
                }

                BankReconciliationHelper.ValidateMatch(
                    reconciliation,
                    line,
                    transaction);

                var alreadyMatched = await _repository.QueryLines()
                    .AnyAsync(
                        item =>
                            item.Id != line.Id &&
                            item.MatchedBankTransactionId == transaction.Id,
                        cancellationToken);

                if (alreadyMatched)
                {
                    throw new InvalidOperationException(
                        "This bank transaction is already matched to another statement entry.");
                }

                line.MatchedBankTransactionId = transaction.Id;
                line.MatchedBankTransaction = transaction;
                line.MatchedDate = DateTime.UtcNow;
            }
            else
            {
                line.MatchedBankTransactionId = null;
                line.MatchedBankTransaction = null;
                line.MatchedDate = null;
            }

            reconciliation.ModifiedDate = DateTime.UtcNow;

            await _repository.SaveChangesAsync(
                cancellationToken);

            var response = await ToResponseAsync(
                reconciliation,
                cancellationToken);

            await databaseTransaction.CommitAsync(
                cancellationToken);

            return response;
        }

        public async Task<BankReconciliationResponseDto?> CompleteAsync(
            int id,
            BankReconciliationVersionDto dto,
            CancellationToken cancellationToken)
        {
            BankReconciliationHelper.Validate(dto);

            await using var databaseTransaction =
                await _repository.BeginTransactionAsync(
                    cancellationToken);

            var accountId = await _repository.Query().Where(row => row.Id == id)
                .Select(row => (int?)row.BankAccountId).SingleOrDefaultAsync(cancellationToken);
            if (accountId == null) return null;
            await BankingPaymentLock.AcquireAsync(_context, "Account." + accountId.Value, cancellationToken);

            var reconciliation = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (reconciliation == null)
            {
                return null;
            }

            if (reconciliation.Status == "Completed")
            {
                var completedResponse = await ToResponseAsync(
                    reconciliation,
                    cancellationToken);

                await databaseTransaction.CommitAsync(
                    cancellationToken);

                return completedResponse;
            }

            ValidateSnapshot(reconciliation);

            BankReconciliationHelper.CheckDraft(reconciliation);

            BankReconciliationHelper.CheckVersion(
                reconciliation.RowVersion,
                dto.RowVersion);

            await ValidateSequenceAsync(
                reconciliation.BankAccount,
                reconciliation.PeriodStart,
                reconciliation.StatementOpeningBalance,
                cancellationToken);

            var response = await ToResponseAsync(
                reconciliation,
                cancellationToken);

            if (response.StatementBalanceDifference != 0)
            {
                throw new ValidationException(
                    "The imported entries do not agree with the statement closing balance.");
            }

            if (response.UnmatchedStatementLineCount != 0)
            {
                throw new ValidationException(
                    "Match every bank statement entry before completing reconciliation.");
            }

            if (response.Difference != 0)
            {
                throw new ValidationException(
                    "The reconciliation difference must be zero.");
            }

            var now = DateTime.UtcNow;

            reconciliation.Status = "Completed";
            reconciliation.CompletedDate = now;
            reconciliation.ModifiedDate = now;

            await _repository.SaveChangesAsync(
                cancellationToken);

            response.Status = reconciliation.Status;
            response.CompletedDate = reconciliation.CompletedDate;
            response.ModifiedDate = reconciliation.ModifiedDate;
            response.RowVersion = reconciliation.RowVersion;

            await databaseTransaction.CommitAsync(
                cancellationToken);

            return response;
        }

        private async Task ValidateSequenceAsync(
            BankAccount account,
            DateOnly periodStart,
            decimal statementOpeningBalance,
            CancellationToken cancellationToken)
        {
            var previous = await _repository.Query()
                .Where(reconciliation =>
                    reconciliation.BankAccountId == account.Id &&
                    reconciliation.Status == "Completed")
                .OrderByDescending(reconciliation => reconciliation.PeriodEnd)
                .FirstOrDefaultAsync(cancellationToken);

            if (previous == null)
            {
                if (periodStart != account.OpeningBalanceDate!.Value)
                {
                    throw new ValidationException(
                        "The first statement must start on the account opening balance date.");
                }

                if (statementOpeningBalance != account.OpeningBalance)
                {
                    throw new ValidationException(
                        "The first statement opening balance must match the configured account opening balance.");
                }

                return;
            }

            if (periodStart.DayNumber != previous.PeriodEnd.DayNumber + 1)
            {
                throw new ValidationException(
                    "The statement must start on the day after the previous completed statement ends.");
            }

            if (statementOpeningBalance != previous.StatementClosingBalance)
            {
                throw new ValidationException(
                    "The opening balance must match the previous statement closing balance.");
            }
        }

        private static void ValidateAccount(
            BankAccount account)
        {
            if (!account.OpeningBalanceDate.HasValue)
            {
                throw new ValidationException(
                    "Configure the bank account opening balance first.");
            }

            if (
                string.IsNullOrWhiteSpace(account.Currency) ||
                account.Currency.Length != 3 ||
                account.CurrencyDecimalPlaces < 0 ||
                account.CurrencyDecimalPlaces > 4)
            {
                throw new ValidationException(
                    "The account has invalid currency settings.");
            }

            // Historical statements can be reconciled for inactive accounts.
        }

        private static void ValidateSnapshot(
            BankReconciliation reconciliation)
        {
            var account = reconciliation.BankAccount;

            ValidateAccount(account);

            if (
                account.Id != reconciliation.BankAccountId ||
                account.CompanyId != reconciliation.CompanyId ||
                account.Currency != reconciliation.Currency ||
                account.CurrencyDecimalPlaces !=
                    reconciliation.CurrencyDecimalPlaces)
            {
                throw new ValidationException(
                    "The account no longer matches the saved reconciliation settings.");
            }
        }

        private async Task<BankReconciliationResponseDto> ToResponseAsync(
            BankReconciliation reconciliation,
            CancellationToken cancellationToken)
        {
            ValidateSnapshot(reconciliation);

            var account = reconciliation.BankAccount;
            var openingDate = account.OpeningBalanceDate!.Value;

            var books = await _transactions.Query()
                .Where(transaction =>
                    transaction.BankAccountId == account.Id &&
                    transaction.CompanyId == account.CompanyId &&
                    transaction.Status == "Posted" &&
                    transaction.TransactionDate >= openingDate &&
                    transaction.TransactionDate <= reconciliation.PeriodEnd)
                .OrderBy(transaction => transaction.TransactionDate)
                .ThenBy(transaction => transaction.Id)
                .ToListAsync(cancellationToken);

            if (books.Any(transaction =>
                transaction.Currency != reconciliation.Currency ||
                transaction.CurrencyDecimalPlaces !=
                    reconciliation.CurrencyDecimalPlaces))
            {
                throw new ValidationException(
                    "An account transaction has inconsistent currency settings.");
            }

            var booksById = books.ToDictionary(
                transaction => transaction.Id);

            foreach (var line in reconciliation.Lines)
            {
                if (!line.MatchedBankTransactionId.HasValue)
                {
                    continue;
                }

                if (!booksById.TryGetValue(
                    line.MatchedBankTransactionId.Value,
                    out var matched))
                {
                    throw new ValidationException(
                        "A matched transaction is no longer eligible for this statement.");
                }

                BankReconciliationHelper.ValidateMatch(
                    reconciliation,
                    line,
                    matched);
            }

            // Only earlier completed statements and this statement
            // count as cleared at this statement's cutoff.
            var previouslyClearedIds = await _repository.QueryLines()
                .Where(line =>
                    line.MatchedBankTransactionId.HasValue &&
                    line.BankReconciliationId != reconciliation.Id &&
                    line.BankReconciliation.BankAccountId == account.Id &&
                    line.BankReconciliation.Status == "Completed" &&
                    line.BankReconciliation.PeriodEnd <=
                        reconciliation.PeriodEnd)
                .Select(line => line.MatchedBankTransactionId!.Value)
                .ToListAsync(cancellationToken);

            var clearedIds = previouslyClearedIds.ToHashSet();

            foreach (var line in reconciliation.Lines)
            {
                if (line.MatchedBankTransactionId.HasValue)
                {
                    clearedIds.Add(line.MatchedBankTransactionId.Value);
                }
            }

            var moneyIn = books
                .Where(transaction =>
                    transaction.Direction == BankTransactionDirection.MoneyIn)
                .Sum(transaction => transaction.Amount);

            var moneyOut = books
                .Where(transaction =>
                    transaction.Direction == BankTransactionDirection.MoneyOut)
                .Sum(transaction => transaction.Amount);

            var outstandingDeposits = books
                .Where(transaction =>
                    !clearedIds.Contains(transaction.Id) &&
                    transaction.Direction == BankTransactionDirection.MoneyIn)
                .Sum(transaction => transaction.Amount);

            var outstandingWithdrawals = books
                .Where(transaction =>
                    !clearedIds.Contains(transaction.Id) &&
                    transaction.Direction == BankTransactionDirection.MoneyOut)
                .Sum(transaction => transaction.Amount);

            var statementMoneyIn = reconciliation.Lines
                .Where(line =>
                    line.Direction == BankTransactionDirection.MoneyIn)
                .Sum(line => line.Amount);

            var statementMoneyOut = reconciliation.Lines
                .Where(line =>
                    line.Direction == BankTransactionDirection.MoneyOut)
                .Sum(line => line.Amount);

            var calculatedClosingBalance =
                reconciliation.StatementOpeningBalance +
                statementMoneyIn -
                statementMoneyOut;

            var bookBalance =
                account.OpeningBalance +
                moneyIn -
                moneyOut;

            var adjustedStatementBalance =
                reconciliation.StatementClosingBalance +
                outstandingDeposits -
                outstandingWithdrawals;

            // Transactions matched elsewhere are unavailable for selection.
            var reservedElsewhere = await _repository.QueryLines()
                .Where(line =>
                    line.BankReconciliationId != reconciliation.Id &&
                    line.MatchedBankTransactionId.HasValue &&
                    line.BankReconciliation.BankAccountId == account.Id)
                .Select(line => line.MatchedBankTransactionId!.Value)
                .ToListAsync(cancellationToken);

            var reservedIds = reservedElsewhere.ToHashSet();

            var companies = await _accounts.GetCompaniesAsync(
                cancellationToken);

            var names = companies.ToDictionary(
                company => company.Id,
                company => company.Name);

            var response = MapHeader(
                reconciliation,
                names);

            response.StatementCalculatedClosingBalance =
                calculatedClosingBalance;

            response.StatementBalanceDifference =
                reconciliation.StatementClosingBalance -
                calculatedClosingBalance;

            response.BookBalance = bookBalance;
            response.OutstandingDeposits = outstandingDeposits;
            response.OutstandingWithdrawals = outstandingWithdrawals;
            response.AdjustedStatementBalance = adjustedStatementBalance;
            response.Difference = bookBalance - adjustedStatementBalance;

            response.UnmatchedStatementLineCount =
                reconciliation.Lines.Count(
                    line => !line.MatchedBankTransactionId.HasValue);

            response.Lines = reconciliation.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line => new BankStatementLineResponseDto
                {
                    Id = line.Id,
                    LineNumber = line.LineNumber,
                    TransactionDate = line.TransactionDate,
                    Direction = line.Direction,
                    Amount = line.Amount,
                    Reference = line.Reference,
                    Description = line.Description,
                    MatchedBankTransactionId = line.MatchedBankTransactionId,
                    MatchedDate = line.MatchedDate,
                    RowVersion = line.RowVersion
                })
                .ToList();

            response.Books = books
                .Where(transaction => !reservedIds.Contains(transaction.Id))
                .Select(transaction => new BankTransactionResponseDto
                {
                    Id = transaction.Id,
                    RequestId = transaction.RequestId,
                    BankAccountId = account.Id,
                    BankAccountName = account.Name,
                    CompanyId = account.CompanyId,
                    CompanyName = response.CompanyName,
                    TransactionDate = transaction.TransactionDate,
                    Direction = transaction.Direction,
                    Amount = transaction.Amount,
                    Currency = transaction.Currency,
                    CurrencyDecimalPlaces = transaction.CurrencyDecimalPlaces,
                    Status = transaction.Status,
                    Reference = transaction.Reference,
                    Description = transaction.Description,
                    Notes = transaction.Notes,
                    ReversalOfTransactionId =
                        transaction.ReversalOfTransactionId,
                    CreatedDate = transaction.CreatedDate,
                    ModifiedDate = transaction.ModifiedDate,
                    PostedDate = transaction.PostedDate,
                    CancelledDate = transaction.CancelledDate,
                    RowVersion = transaction.RowVersion
                })
                .ToList();

            return response;
        }

        private static BankReconciliationResponseDto MapHeader(
            BankReconciliation reconciliation,
            IReadOnlyDictionary<int, string> companies)
        {
            companies.TryGetValue(
                reconciliation.CompanyId,
                out var companyName);

            return new BankReconciliationResponseDto
            {
                Id = reconciliation.Id,
                RequestId = reconciliation.RequestId,
                BankAccountId = reconciliation.BankAccountId,
                BankAccountName = reconciliation.BankAccount.Name,
                CompanyId = reconciliation.CompanyId,
                CompanyName = companyName ?? "",
                Currency = reconciliation.Currency,
                CurrencyDecimalPlaces = reconciliation.CurrencyDecimalPlaces,
                PeriodStart = reconciliation.PeriodStart,
                PeriodEnd = reconciliation.PeriodEnd,
                StatementOpeningBalance =
                    reconciliation.StatementOpeningBalance,
                StatementClosingBalance =
                    reconciliation.StatementClosingBalance,
                Status = reconciliation.Status,
                Notes = reconciliation.Notes,
                CreatedDate = reconciliation.CreatedDate,
                ModifiedDate = reconciliation.ModifiedDate,
                CompletedDate = reconciliation.CompletedDate,
                RowVersion = reconciliation.RowVersion
            };
        }
    }
}