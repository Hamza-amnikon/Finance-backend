using System.ComponentModel.DataAnnotations;
using BankingService.API.DTOs;
using BankingService.API.Models;
using BankingService.API.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Services
{
    public class BankTransferService : IBankTransferService
    {
        private readonly IBankTransferRepository _repository;
        private readonly IBankAccountRepository _accounts;
        private readonly IBankTransactionRepository _transactions;

        public BankTransferService(
            IBankTransferRepository repository,
            IBankAccountRepository accounts,
            IBankTransactionRepository transactions)
        {
            _repository = repository;
            _accounts = accounts;
            _transactions = transactions;
        }

        public async Task<BankTransferListDto> GetPagedAsync(
            BankTransferQueryDto query,
            CancellationToken cancellationToken)
        {
            BankTransferHelper.Validate(query);

            var transfers = _repository.Query();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();

                transfers = transfers.Where(transfer =>
                    transfer.Reference.Contains(search) ||
                    transfer.Notes.Contains(search) ||
                    transfer.SourceBankAccount.Name.Contains(search) ||
                    transfer.DestinationBankAccount.Name.Contains(search));
            }

            if (query.SourceBankAccountId.HasValue)
            {
                transfers = transfers.Where(transfer =>
                    transfer.SourceBankAccountId ==
                        query.SourceBankAccountId.Value);
            }

            if (query.DestinationBankAccountId.HasValue)
            {
                transfers = transfers.Where(transfer =>
                    transfer.DestinationBankAccountId ==
                        query.DestinationBankAccountId.Value);
            }

            if (query.CompanyId.HasValue)
            {
                transfers = transfers.Where(transfer =>
                    transfer.SourceCompanyId == query.CompanyId.Value ||
                    transfer.DestinationCompanyId == query.CompanyId.Value);
            }

            if (query.Status.HasValue)
            {
                transfers = transfers.Where(transfer =>
                    transfer.Status == query.Status.Value);
            }

            if (query.From.HasValue)
            {
                transfers = transfers.Where(transfer =>
                    transfer.TransferDate >= query.From.Value);
            }

            if (query.To.HasValue)
            {
                transfers = transfers.Where(transfer =>
                    transfer.TransferDate <= query.To.Value);
            }

            var total = await transfers.CountAsync(
                cancellationToken);

            var lastPage = Math.Max(
                1,
                (int)Math.Ceiling(total / (double)query.PageSize));

            var page = Math.Min(
                query.Page,
                lastPage);

            var records = await transfers
                .Include(transfer => transfer.SourceBankAccount)
                .Include(transfer => transfer.DestinationBankAccount)
                .OrderByDescending(transfer => transfer.TransferDate)
                .ThenByDescending(transfer => transfer.Id)
                .Skip((page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            var companies = await GetCompanyNamesAsync(
                cancellationToken);

            return new BankTransferListDto
            {
                Data = records
                    .Select(transfer => ToResponse(transfer, companies))
                    .ToList(),

                Total = total,
                Page = page,
                PageSize = query.PageSize
            };
        }

        public async Task<BankTransferResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var transfer = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (transfer == null)
            {
                return null;
            }

            return await ToResponseAsync(
                transfer,
                cancellationToken);
        }

        public async Task<BankTransferResponseDto> CreateAsync(
            CreateBankTransferDto dto,
            CancellationToken cancellationToken)
        {
            BankTransferHelper.Validate(dto);

            var fingerprint = BankTransferHelper.CreateFingerprint(dto);

            var existing = await _repository.GetByRequestIdAsync(
                dto.RequestId,
                cancellationToken);

            if (existing != null)
            {
                CheckFingerprint(existing, fingerprint);

                return await ToResponseAsync(
                    existing,
                    cancellationToken);
            }

            var source = await _accounts.GetByIdAsync(
                dto.SourceBankAccountId,
                cancellationToken);

            var destination = await _accounts.GetByIdAsync(
                dto.DestinationBankAccountId,
                cancellationToken);

            if (source == null || destination == null)
            {
                throw new ValidationException(
                    "Source or destination bank account was not found.");
            }

            if (source.Id == destination.Id)
            {
                throw new ValidationException(
                    "Source and destination accounts must be different.");
            }

            BankTransferHelper.ValidateAccount(source, requireActive: true);
            BankTransferHelper.ValidateAccount(destination, requireActive: true);
            BankTransferHelper.ValidatePostingDate(source, dto.TransferDate);

            ValidateSourceAmounts(
                dto.SourceAmount,
                dto.SourceFeeAmount,
                source.CurrencyDecimalPlaces);

            var transfer = new BankTransfer
            {
                RequestId = dto.RequestId,
                RequestFingerprint = fingerprint,

                SourceBankAccountId = source.Id,
                SourceBankAccount = source,
                SourceCompanyId = source.CompanyId,
                SourceCurrency = source.Currency,
                SourceCurrencyDecimalPlaces = source.CurrencyDecimalPlaces,

                DestinationBankAccountId = destination.Id,
                DestinationBankAccount = destination,
                DestinationCompanyId = destination.CompanyId,
                DestinationCurrency = destination.Currency,
                DestinationCurrencyDecimalPlaces =
                    destination.CurrencyDecimalPlaces,

                SourceAmount = dto.SourceAmount,
                SourceFeeAmount = dto.SourceFeeAmount,
                TransferDate = dto.TransferDate,
                Reference = dto.Reference?.Trim() ?? "",
                Notes = dto.Notes?.Trim() ?? "",

                Status = BankTransferStatus.Draft,
                CreatedDate = DateTime.UtcNow
            };

            await _repository.AddAsync(
                transfer,
                cancellationToken);

            try
            {
                await _repository.SaveChangesAsync(
                    cancellationToken);
            }
            catch (DbUpdateException exception)
                when (IsUniqueViolation(exception))
            {
                // Another request may have created the same transfer
                // while this request was being processed.
                var duplicate = await _repository.Query()
                    .Include(item => item.SourceBankAccount)
                    .Include(item => item.DestinationBankAccount)
                    .FirstOrDefaultAsync(
                        item => item.RequestId == dto.RequestId,
                        cancellationToken);

                if (duplicate == null)
                {
                    throw;
                }

                CheckFingerprint(duplicate, fingerprint);

                return await ToResponseAsync(
                    duplicate,
                    cancellationToken);
            }

            return await ToResponseAsync(
                transfer,
                cancellationToken);
        }

        public async Task<BankTransferResponseDto?> UpdateAsync(
            int id,
            UpdateBankTransferDto dto,
            CancellationToken cancellationToken)
        {
            BankTransferHelper.Validate(dto);

            var transfer = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (transfer == null)
            {
                return null;
            }

            BankTransferHelper.CheckVersion(transfer, dto.RowVersion);
            BankTransferHelper.CheckStatus(
                transfer,
                BankTransferStatus.Draft);

            BankTransferHelper.ValidateSnapshot(transfer);

            BankTransferHelper.ValidateAccount(
                transfer.SourceBankAccount,
                requireActive: true);

            BankTransferHelper.ValidateAccount(
                transfer.DestinationBankAccount,
                requireActive: true);

            BankTransferHelper.ValidatePostingDate(
                transfer.SourceBankAccount,
                dto.TransferDate);

            ValidateSourceAmounts(
                dto.SourceAmount,
                dto.SourceFeeAmount,
                transfer.SourceCurrencyDecimalPlaces);

            transfer.SourceAmount = dto.SourceAmount;
            transfer.SourceFeeAmount = dto.SourceFeeAmount;
            transfer.TransferDate = dto.TransferDate;
            transfer.Reference = dto.Reference?.Trim() ?? "";
            transfer.Notes = dto.Notes?.Trim() ?? "";
            transfer.ModifiedDate = DateTime.UtcNow;

            // Keep the original creation fingerprint for request retries.
            await _repository.SaveChangesAsync(
                cancellationToken);

            return await ToResponseAsync(
                transfer,
                cancellationToken);
        }

        public async Task<BankTransferResponseDto?> ConfirmSentAsync(
            int id,
            ConfirmBankTransferSentDto dto,
            CancellationToken cancellationToken)
        {
            BankTransferHelper.Validate(dto);

            var transfer = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (transfer == null)
            {
                return null;
            }

            var reference = dto.Reference?.Trim() ?? "";

            // A retry of an already successful confirmation must not
            // create another debit.
            if (
                transfer.Status == BankTransferStatus.Pending ||
                transfer.Status == BankTransferStatus.Completed)
            {
                if (
                    transfer.SourceAmount != dto.SourceAmount ||
                    transfer.SourceFeeAmount != dto.SourceFeeAmount ||
                    transfer.TransferDate != dto.TransferDate ||
                    transfer.Reference != reference)
                {
                    throw new InvalidOperationException(
                        "This transfer was already sent with different details.");
                }

                CheckSentLinks(transfer);

                return await ToResponseAsync(
                    transfer,
                    cancellationToken);
            }

            BankTransferHelper.CheckVersion(transfer, dto.RowVersion);
            BankTransferHelper.CheckStatus(
                transfer,
                BankTransferStatus.Draft);

            BankTransferHelper.ValidateSnapshot(transfer);

            BankTransferHelper.ValidateAccount(
                transfer.SourceBankAccount,
                requireActive: true);

            BankTransferHelper.ValidateAccount(
                transfer.DestinationBankAccount,
                requireActive: true);

            BankTransferHelper.ValidatePostingDate(
                transfer.SourceBankAccount,
                dto.TransferDate);

            ValidateSourceAmounts(
                dto.SourceAmount,
                dto.SourceFeeAmount,
                transfer.SourceCurrencyDecimalPlaces);

            if (
                transfer.SourceTransactionId.HasValue ||
                transfer.SourceFeeTransactionId.HasValue ||
                transfer.DestinationTransactionId.HasValue)
            {
                throw new InvalidOperationException(
                    "This draft already has linked transactions. Review it before continuing.");
            }

            var now = DateTime.UtcNow;

            await using var databaseTransaction =
                await _repository.BeginTransactionAsync(
                    cancellationToken);

            transfer.SourceAmount = dto.SourceAmount;
            transfer.SourceFeeAmount = dto.SourceFeeAmount;
            transfer.TransferDate = dto.TransferDate;
            transfer.Reference = reference;
            transfer.Status = BankTransferStatus.Pending;
            transfer.SentDate = now;
            transfer.ModifiedDate = now;

            // Update the transfer first so its row version protects
            // against simultaneous confirmations.
            await _repository.SaveChangesAsync(
                cancellationToken);

            var debit = CreatePostedTransaction(
                transfer.SourceBankAccount,
                transfer.TransferDate,
                BankTransactionDirection.MoneyOut,
                transfer.SourceAmount,
                transfer.Reference,
                $"Transfer BT-{transfer.Id}: money sent",
                transfer.Notes,
                now);

            await _transactions.AddAsync(
                debit,
                cancellationToken);

            BankTransaction? fee = null;

            if (transfer.SourceFeeAmount > 0)
            {
                fee = CreatePostedTransaction(
                    transfer.SourceBankAccount,
                    transfer.TransferDate,
                    BankTransactionDirection.MoneyOut,
                    transfer.SourceFeeAmount,
                    transfer.Reference,
                    $"Transfer BT-{transfer.Id}: source bank fee",
                    transfer.Notes,
                    now);

                await _transactions.AddAsync(
                    fee,
                    cancellationToken);
            }

            // All repositories share the same scoped BankingDbContext.
            await _repository.SaveChangesAsync(
                cancellationToken);

            transfer.SourceTransactionId = debit.Id;
            transfer.SourceFeeTransactionId = fee?.Id;

            await _repository.SaveChangesAsync(
                cancellationToken);

            await databaseTransaction.CommitAsync(
                cancellationToken);

            return await ToResponseAsync(
                transfer,
                cancellationToken);
        }

        public async Task<BankTransferResponseDto?> ConfirmReceivedAsync(
            int id,
            ConfirmBankTransferReceivedDto dto,
            CancellationToken cancellationToken)
        {
            BankTransferHelper.Validate(dto);

            var transfer = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (transfer == null)
            {
                return null;
            }

            // A retry of an already successful receipt must not
            // create another credit.
            if (transfer.Status == BankTransferStatus.Completed)
            {
                if (
                    transfer.DestinationAmount != dto.DestinationAmount ||
                    transfer.ReceivedDate != dto.ReceivedDate)
                {
                    throw new InvalidOperationException(
                        "This transfer was already received with different details.");
                }

                CheckSentLinks(transfer);

                if (!transfer.DestinationTransactionId.HasValue)
                {
                    throw new InvalidOperationException(
                        "The completed transfer is missing its destination transaction.");
                }

                return await ToResponseAsync(
                    transfer,
                    cancellationToken);
            }

            BankTransferHelper.CheckVersion(transfer, dto.RowVersion);
            BankTransferHelper.CheckStatus(
                transfer,
                BankTransferStatus.Pending);

            BankTransferHelper.ValidateSnapshot(transfer);
            CheckSentLinks(transfer);

            // Receipt records money that has already arrived.
            // An account becoming inactive after sending must not
            // prevent recording that receipt.
            BankTransferHelper.ValidateAccount(
                transfer.DestinationBankAccount,
                requireActive: false);

            BankTransferHelper.ValidatePostingDate(
                transfer.DestinationBankAccount,
                dto.ReceivedDate);

            BankTransferHelper.ValidateAmount(
                dto.DestinationAmount,
                transfer.DestinationCurrencyDecimalPlaces,
                "Received amount");

            if (transfer.DestinationTransactionId.HasValue)
            {
                throw new InvalidOperationException(
                    "This transfer already has a destination transaction.");
            }

            var effectiveRate =
                BankTransferHelper.CalculateEffectiveRate(
                    transfer.SourceAmount,
                    dto.DestinationAmount);

            var now = DateTime.UtcNow;

            await using var databaseTransaction =
                await _repository.BeginTransactionAsync(
                    cancellationToken);

            transfer.DestinationAmount = dto.DestinationAmount;
            transfer.ReceivedDate = dto.ReceivedDate;
            transfer.EffectiveExchangeRate = effectiveRate;
            transfer.Status = BankTransferStatus.Completed;
            transfer.CompletedDate = now;
            transfer.ModifiedDate = now;

            await _repository.SaveChangesAsync(
                cancellationToken);

            var credit = CreatePostedTransaction(
                transfer.DestinationBankAccount,
                dto.ReceivedDate,
                BankTransactionDirection.MoneyIn,
                dto.DestinationAmount,
                transfer.Reference,
                $"Transfer BT-{transfer.Id}: money received",
                transfer.Notes,
                now);

            await _transactions.AddAsync(
                credit,
                cancellationToken);

            await _repository.SaveChangesAsync(
                cancellationToken);

            transfer.DestinationTransactionId = credit.Id;

            await _repository.SaveChangesAsync(
                cancellationToken);

            await databaseTransaction.CommitAsync(
                cancellationToken);

            return await ToResponseAsync(
                transfer,
                cancellationToken);
        }

        public async Task<BankTransferResponseDto?> CancelAsync(
            int id,
            BankTransferVersionDto dto,
            CancellationToken cancellationToken)
        {
            BankTransferHelper.Validate(dto);

            var transfer = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (transfer == null)
            {
                return null;
            }

            if (transfer.Status == BankTransferStatus.Cancelled)
            {
                return await ToResponseAsync(
                    transfer,
                    cancellationToken);
            }

            BankTransferHelper.CheckVersion(transfer, dto.RowVersion);
            BankTransferHelper.CheckStatus(
                transfer,
                BankTransferStatus.Draft);

            if (
                transfer.SourceTransactionId.HasValue ||
                transfer.SourceFeeTransactionId.HasValue ||
                transfer.DestinationTransactionId.HasValue)
            {
                throw new InvalidOperationException(
                    "A transfer with linked transactions cannot be cancelled.");
            }

            var now = DateTime.UtcNow;

            transfer.Status = BankTransferStatus.Cancelled;
            transfer.CancelledDate = now;
            transfer.ModifiedDate = now;

            await _repository.SaveChangesAsync(
                cancellationToken);

            return await ToResponseAsync(
                transfer,
                cancellationToken);
        }

        private static void ValidateSourceAmounts(
            decimal amount,
            decimal fee,
            int decimalPlaces)
        {
            BankTransferHelper.ValidateAmount(
                amount,
                decimalPlaces,
                "Source amount");

            BankTransferHelper.ValidateAmount(
                fee,
                decimalPlaces,
                "Source fee",
                allowZero: true);
        }

        private static void CheckFingerprint(
            BankTransfer transfer,
            string fingerprint)
        {
            if (transfer.RequestFingerprint != fingerprint)
            {
                throw new InvalidOperationException(
                    "This request ID was already used for different transfer details.");
            }
        }

        private static void CheckSentLinks(
            BankTransfer transfer)
        {
            if (
                !transfer.SourceTransactionId.HasValue ||
                (
                    transfer.SourceFeeAmount > 0 &&
                    !transfer.SourceFeeTransactionId.HasValue
                ) ||
                (
                    transfer.SourceFeeAmount == 0 &&
                    transfer.SourceFeeTransactionId.HasValue
                ))
            {
                throw new InvalidOperationException(
                    "The transfer's source transaction links are inconsistent.");
            }
        }

        private static bool IsUniqueViolation(
            DbUpdateException exception)
        {
            return exception.InnerException is SqlException sqlException &&
                (sqlException.Number == 2601 || sqlException.Number == 2627);
        }

        private static BankTransaction CreatePostedTransaction(
            BankAccount account,
            DateOnly date,
            BankTransactionDirection direction,
            decimal amount,
            string reference,
            string description,
            string notes,
            DateTime now)
        {
            return new BankTransaction
            {
                RequestId = Guid.NewGuid(),

                // These entries are created internally. Their duplicate
                // protection is the transfer's row version and atomic save.
                RequestFingerprint = "",

                BankAccountId = account.Id,
                BankAccount = account,
                CompanyId = account.CompanyId,
                TransactionDate = date,
                Direction = direction,
                Amount = amount,
                Currency = account.Currency,
                CurrencyDecimalPlaces = account.CurrencyDecimalPlaces,
                Status = "Posted",
                Reference = reference,
                Description = description,
                Notes = notes,
                CreatedDate = now,
                PostedDate = now
            };
        }

        private async Task<Dictionary<int, string>> GetCompanyNamesAsync(
            CancellationToken cancellationToken)
        {
            var companies = await _accounts.GetCompaniesAsync(
                cancellationToken);

            return companies.ToDictionary(
                company => company.Id,
                company => company.Name);
        }

        private async Task<BankTransferResponseDto> ToResponseAsync(
            BankTransfer transfer,
            CancellationToken cancellationToken)
        {
            var companies = await GetCompanyNamesAsync(
                cancellationToken);

            return ToResponse(
                transfer,
                companies);
        }

        private static BankTransferResponseDto ToResponse(
            BankTransfer transfer,
            IReadOnlyDictionary<int, string> companies)
        {
            companies.TryGetValue(
                transfer.SourceCompanyId,
                out var sourceCompanyName);

            companies.TryGetValue(
                transfer.DestinationCompanyId,
                out var destinationCompanyName);

            return new BankTransferResponseDto
            {
                Id = transfer.Id,
                RequestId = transfer.RequestId,

                SourceBankAccountId = transfer.SourceBankAccountId,
                SourceBankAccountName = transfer.SourceBankAccount.Name,
                SourceCompanyId = transfer.SourceCompanyId,
                SourceCompanyName = sourceCompanyName ?? "",
                SourceCurrency = transfer.SourceCurrency,
                SourceCurrencyDecimalPlaces =
                    transfer.SourceCurrencyDecimalPlaces,

                DestinationBankAccountId = transfer.DestinationBankAccountId,
                DestinationBankAccountName =
                    transfer.DestinationBankAccount.Name,
                DestinationCompanyId = transfer.DestinationCompanyId,
                DestinationCompanyName = destinationCompanyName ?? "",
                DestinationCurrency = transfer.DestinationCurrency,
                DestinationCurrencyDecimalPlaces =
                    transfer.DestinationCurrencyDecimalPlaces,

                SourceAmount = transfer.SourceAmount,
                SourceFeeAmount = transfer.SourceFeeAmount,
                DestinationAmount = transfer.DestinationAmount,
                EffectiveExchangeRate = transfer.EffectiveExchangeRate,

                TransferDate = transfer.TransferDate,
                ReceivedDate = transfer.ReceivedDate,
                Status = transfer.Status,
                Reference = transfer.Reference,
                Notes = transfer.Notes,

                SourceTransactionId = transfer.SourceTransactionId,
                SourceFeeTransactionId = transfer.SourceFeeTransactionId,
                DestinationTransactionId = transfer.DestinationTransactionId,

                CreatedDate = transfer.CreatedDate,
                ModifiedDate = transfer.ModifiedDate,
                SentDate = transfer.SentDate,
                CompletedDate = transfer.CompletedDate,
                CancelledDate = transfer.CancelledDate,
                RowVersion = transfer.RowVersion
            };
        }
    }
}