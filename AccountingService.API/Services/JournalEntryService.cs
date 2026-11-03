using System.ComponentModel.DataAnnotations;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using AccountingService.API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Services
{
    public class JournalEntryService
        : IJournalEntryService
    {
        private readonly IJournalEntryRepository
            _repository;

        public JournalEntryService(
            IJournalEntryRepository repository)
        {
            _repository = repository;
        }

        public async Task<JournalEntryPagedResultDto>
            GetPagedAsync(
                JournalEntryQueryDto query,
                CancellationToken cancellationToken)
        {
            Validate(query);

            if (
                query.FromDate.HasValue &&
                query.ToDate.HasValue &&
                query.FromDate.Value >
                query.ToDate.Value)
            {
                throw new ValidationException(
                    "From date cannot be later than to date.");
            }

            await ValidateCompanyAsync(
                query.CompanyId,
                cancellationToken);

            var result =
                await _repository.GetAllAsync(
                    query,
                    cancellationToken);

            return new JournalEntryPagedResultDto
            {
                Data = result.JournalEntries
                    .Select(ToListResponse)
                    .ToList(),

                Total = result.Total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<JournalEntryResponseDto?>
            GetByIdAsync(
                int id,
                int companyId,
                CancellationToken cancellationToken)
        {
            var journalEntry =
                await _repository.GetByIdAsync(
                    id,
                    companyId,
                    cancellationToken);

            return journalEntry == null
                ? null
                : ToResponse(journalEntry);
        }

        public async Task<JournalEntryResponseDto>
            CreateAsync(
                CreateJournalEntryDto dto,
                CancellationToken cancellationToken)
        {
            Validate(dto);
            ValidateEntryDate(dto.EntryDate);

            if (dto.RequestId == Guid.Empty)
            {
                throw new ValidationException(
                    "Request ID is required.");
            }

            foreach (var line in dto.Lines)
            {
                Validate(line);
            }

            await ValidateCompanyAsync(
                dto.CompanyId,
                cancellationToken);

            var sourceDocumentId =
                dto.RequestId.ToString("N");

            var existingJournal =
                await _repository
                    .GetBySourceDocumentAsync(
                        dto.CompanyId,
                        JournalEntrySource.Manual,
                        sourceDocumentId,
                        cancellationToken);

            if (existingJournal != null)
            {
                var existingResult =
                    await _repository.GetByIdAsync(
                        existingJournal.Id,
                        dto.CompanyId,
                        cancellationToken);

                if (existingResult == null)
                {
                    throw new InvalidOperationException(
                        "The existing journal entry could not be loaded.");
                }

                return ToResponse(existingResult);
            }

            var exchangeRate =
                NormalizeExchangeRate(
                    dto.ExchangeRate);

            var lineResult =
                await BuildLinesAsync(
                    dto.CompanyId,
                    dto.Lines,
                    exchangeRate,
                    cancellationToken);

            var journalEntry = new JournalEntry
            {
                CompanyId = dto.CompanyId,

                EntryNumber =
                    GenerateEntryNumber(
                        "JE",
                        dto.EntryDate),

                EntryDate = dto.EntryDate,

                Branch =
                    NormalizeRequiredText(
                        dto.Branch,
                        "Branch is required."),

                Currency =
                    NormalizeCurrency(
                        dto.Currency),

                ExchangeRate = exchangeRate,

                Narration =
                    NormalizeRequiredText(
                        dto.Narration,
                        "Narration is required."),

                Reference =
                    NormalizeOptionalText(
                        dto.Reference),

                Source =
                    JournalEntrySource.Manual,

                SourceDocumentId =
                    sourceDocumentId,

                Status =
                    JournalEntryStatus.Draft,

                TotalDebit =
                    lineResult.TotalDebit,

                TotalCredit =
                    lineResult.TotalCredit,

                Lines =
                    lineResult.Lines.ToList(),

                CreatedDate =
                    DateTime.UtcNow
            };

            await _repository.AddAsync(
                journalEntry,
                cancellationToken);

            await _repository.SaveChangesAsync(
                cancellationToken);

            var savedJournal =
                await _repository.GetByIdAsync(
                    journalEntry.Id,
                    journalEntry.CompanyId,
                    cancellationToken);

            if (savedJournal == null)
            {
                throw new InvalidOperationException(
                    "The journal entry was saved but could not be reloaded.");
            }

            return ToResponse(savedJournal);
        }

        public async Task<JournalEntryResponseDto?>
            UpdateAsync(
                int id,
                int companyId,
                UpdateJournalEntryDto dto,
                CancellationToken cancellationToken)
        {
            Validate(dto);
            ValidateEntryDate(dto.EntryDate);

            foreach (var line in dto.Lines)
            {
                Validate(line);
            }

            var journalEntry =
                await _repository.GetByIdAsync(
                    id,
                    companyId,
                    cancellationToken);

            if (journalEntry == null)
            {
                return null;
            }

            if (
                journalEntry.Status !=
                JournalEntryStatus.Draft)
            {
                throw new ValidationException(
                    "Only draft journal entries can be edited.");
            }

            var rowVersion =
                DecodeRowVersion(dto.RowVersion);

            EnsureRowVersionMatches(
                journalEntry,
                rowVersion);

            _repository.SetOriginalRowVersion(
                journalEntry,
                rowVersion);

            var exchangeRate =
                NormalizeExchangeRate(
                    dto.ExchangeRate);

            var lineResult =
                await BuildLinesAsync(
                    companyId,
                    dto.Lines,
                    exchangeRate,
                    cancellationToken);

            var existingLines =
                journalEntry.Lines.ToList();

            _repository.RemoveLines(
                existingLines);

            journalEntry.EntryDate =
                dto.EntryDate;

            journalEntry.Branch =
                NormalizeRequiredText(
                    dto.Branch,
                    "Branch is required.");

            journalEntry.Currency =
                NormalizeCurrency(
                    dto.Currency);

            journalEntry.ExchangeRate =
                exchangeRate;

            journalEntry.Narration =
                NormalizeRequiredText(
                    dto.Narration,
                    "Narration is required.");

            journalEntry.Reference =
                NormalizeOptionalText(
                    dto.Reference);

            journalEntry.TotalDebit =
                lineResult.TotalDebit;

            journalEntry.TotalCredit =
                lineResult.TotalCredit;

            journalEntry.Lines =
                lineResult.Lines.ToList();

            journalEntry.ModifiedDate =
                DateTime.UtcNow;

            await _repository.SaveChangesAsync(
                cancellationToken);

            var savedJournal =
                await _repository.GetByIdAsync(
                    journalEntry.Id,
                    journalEntry.CompanyId,
                    cancellationToken);

            if (savedJournal == null)
            {
                throw new InvalidOperationException(
                    "The journal entry was updated but could not be reloaded.");
            }

            return ToResponse(savedJournal);
        }

        public async Task<JournalEntryResponseDto?>
            PostAsync(
                int id,
                int companyId,
                JournalEntryActionDto dto,
                CancellationToken cancellationToken)
        {
            Validate(dto);

            var journalEntry =
                await _repository.GetByIdAsync(
                    id,
                    companyId,
                    cancellationToken);

            if (journalEntry == null)
            {
                return null;
            }

            if (
                journalEntry.Status !=
                JournalEntryStatus.Draft)
            {
                throw new ValidationException(
                    "Only draft journal entries can be posted.");
            }

            var rowVersion =
                DecodeRowVersion(dto.RowVersion);

            EnsureRowVersionMatches(
                journalEntry,
                rowVersion);

            _repository.SetOriginalRowVersion(
                journalEntry,
                rowVersion);

            await ValidateJournalForPostingAsync(
                journalEntry,
                cancellationToken);

            journalEntry.Status =
                JournalEntryStatus.Posted;

            journalEntry.PostedDate =
                DateTime.UtcNow;

            journalEntry.ModifiedDate =
                DateTime.UtcNow;

            await _repository.SaveChangesAsync(
                cancellationToken);

            return ToResponse(journalEntry);
        }

        public async Task<JournalEntryResponseDto?>
            ReverseAsync(
                int id,
                int companyId,
                JournalEntryActionDto dto,
                CancellationToken cancellationToken)
        {
            Validate(dto);

            var reason =
                NormalizeRequiredText(
                    dto.Reason ?? string.Empty,
                    "A reversal reason is required.");

            var originalJournal =
                await _repository.GetByIdAsync(
                    id,
                    companyId,
                    cancellationToken);

            if (originalJournal == null)
            {
                return null;
            }

            if (
                originalJournal.Status !=
                JournalEntryStatus.Posted)
            {
                throw new ValidationException(
                    "Only a posted journal entry can be reversed.");
            }

            var rowVersion =
                DecodeRowVersion(dto.RowVersion);

            EnsureRowVersionMatches(
                originalJournal,
                rowVersion);

            _repository.SetOriginalRowVersion(
                originalJournal,
                rowVersion);

            var reversalExists =
                await _repository.ReversalExistsAsync(
                    originalJournal.Id,
                    companyId,
                    cancellationToken);

            if (reversalExists)
            {
                throw new ValidationException(
                    "This journal entry has already been reversed.");
            }

            var reversalDate =
                DateOnly.FromDateTime(
                    DateTime.UtcNow);

            var reversalLines =
                originalJournal.Lines
                    .OrderBy(line =>
                        line.LineNumber)
                    .Select((line, index) =>
                        new JournalEntryLine
                        {
                            CompanyId =
                                originalJournal.CompanyId,

                            LineNumber =
                                index + 1,

                            AccountId =
                                line.AccountId,

                            Description =
                                BuildReversalDescription(
                                    line.Description),

                            DebitAmount =
                                line.CreditAmount,

                            CreditAmount =
                                line.DebitAmount,

                            BaseDebitAmount =
                                line.BaseCreditAmount,

                            BaseCreditAmount =
                                line.BaseDebitAmount,

                            CostCenter =
                                line.CostCenter,

                            CounterpartyType =
                                line.CounterpartyType,

                            CounterpartyId =
                                line.CounterpartyId
                        })
                    .ToList();

            var reversalJournal =
                new JournalEntry
                {
                    CompanyId =
                        originalJournal.CompanyId,

                    EntryNumber =
                        GenerateEntryNumber(
                            "RV",
                            reversalDate),

                    EntryDate =
                        reversalDate,

                    Branch =
                        originalJournal.Branch,

                    Currency =
                        originalJournal.Currency,

                    ExchangeRate =
                        originalJournal.ExchangeRate,

                    Narration =
                        BuildReversalNarration(
                            originalJournal.EntryNumber,
                            reason),

                    Reference =
                        originalJournal.EntryNumber,

                    Source =
                        JournalEntrySource.Reversal,

                    SourceDocumentId =
                        originalJournal.Id.ToString(),

                    SourceDocumentNumber =
                        originalJournal.EntryNumber,

                    Status =
                        JournalEntryStatus.Posted,

                    TotalDebit =
                        originalJournal.TotalCredit,

                    TotalCredit =
                        originalJournal.TotalDebit,

                    ReversalOfJournalEntryId =
                        originalJournal.Id,

                    Lines =
                        reversalLines,

                    CreatedDate =
                        DateTime.UtcNow,

                    PostedDate =
                        DateTime.UtcNow
                };

            originalJournal.Status =
                JournalEntryStatus.Reversed;

            originalJournal.ReversedDate =
                DateTime.UtcNow;

            originalJournal.ModifiedDate =
                DateTime.UtcNow;

            await _repository.AddAsync(
                reversalJournal,
                cancellationToken);

            await _repository.SaveChangesAsync(
                cancellationToken);

            var savedReversal =
                await _repository.GetByIdAsync(
                    reversalJournal.Id,
                    reversalJournal.CompanyId,
                    cancellationToken);

            if (savedReversal == null)
            {
                throw new InvalidOperationException(
                    "The reversal was saved but could not be reloaded.");
            }

            return ToResponse(savedReversal);
        }

        private async Task<JournalLineBuildResult>
            BuildLinesAsync(
                int companyId,
                IReadOnlyList<JournalEntryLineDto> lineDtos,
                decimal exchangeRate,
                CancellationToken cancellationToken)
        {
            if (lineDtos.Count < 2)
            {
                throw new ValidationException(
                    "A journal entry must contain at least two lines.");
            }

            var accountIds =
                lineDtos
                    .Select(line =>
                        line.AccountId)
                    .Distinct()
                    .ToList();

            var accounts =
                await _repository.GetAccountsAsync(
                    companyId,
                    accountIds,
                    cancellationToken);

            if (accounts.Count != accountIds.Count)
            {
                throw new ValidationException(
                    "One or more selected accounts do not belong to this company.");
            }

            var accountsById =
                accounts.ToDictionary(
                    account =>
                        account.Id);

            var lines =
                new List<JournalEntryLine>();

            decimal totalDebit = 0;
            decimal totalCredit = 0;

            for (
                var index = 0;
                index < lineDtos.Count;
                index++)
            {
                var dto =
                    lineDtos[index];

                var account =
                    accountsById[dto.AccountId];

                ValidateManualPostingAccount(
                    account);

                var debitAmount =
                    RoundAmount(
                        dto.DebitAmount);

                var creditAmount =
                    RoundAmount(
                        dto.CreditAmount);

                var hasDebit =
                    debitAmount > 0;

                var hasCredit =
                    creditAmount > 0;

                if (hasDebit == hasCredit)
                {
                    throw new ValidationException(
                        $"Line {index + 1} must contain either a debit or a credit amount.");
                }

                totalDebit +=
                    debitAmount;

                totalCredit +=
                    creditAmount;

                lines.Add(
                    new JournalEntryLine
                    {
                        CompanyId =
                            companyId,

                        LineNumber =
                            index + 1,

                        AccountId =
                            account.Id,

                        Description =
                            dto.Description?.Trim() ??
                            string.Empty,

                        DebitAmount =
                            debitAmount,

                        CreditAmount =
                            creditAmount,

                        BaseDebitAmount =
                            RoundAmount(
                                debitAmount *
                                exchangeRate),

                        BaseCreditAmount =
                            RoundAmount(
                                creditAmount *
                                exchangeRate),

                        CostCenter =
                            NormalizeOptionalText(
                                dto.CostCenter),

                        CounterpartyType =
                            NormalizeOptionalText(
                                dto.CounterpartyType),

                        CounterpartyId =
                            NormalizeOptionalText(
                                dto.CounterpartyId)
                    });
            }

            totalDebit =
                RoundAmount(totalDebit);

            totalCredit =
                RoundAmount(totalCredit);

            if (totalDebit <= 0)
            {
                throw new ValidationException(
                    "The journal entry total must be greater than zero.");
            }

            if (totalDebit != totalCredit)
            {
                throw new ValidationException(
                    $"The journal entry is not balanced. " +
                    $"Debit is {totalDebit:0.0000} and " +
                    $"credit is {totalCredit:0.0000}.");
            }

            return new JournalLineBuildResult(
                lines,
                totalDebit,
                totalCredit);
        }

        private async Task
            ValidateJournalForPostingAsync(
                JournalEntry journalEntry,
                CancellationToken cancellationToken)
        {
            if (journalEntry.Lines.Count < 2)
            {
                throw new ValidationException(
                    "A journal entry must contain at least two lines.");
            }

            var totalDebit =
                RoundAmount(
                    journalEntry.Lines.Sum(
                        line =>
                            line.DebitAmount));

            var totalCredit =
                RoundAmount(
                    journalEntry.Lines.Sum(
                        line =>
                            line.CreditAmount));

            if (
                totalDebit <= 0 ||
                totalDebit != totalCredit)
            {
                throw new ValidationException(
                    "The journal entry must be balanced before posting.");
            }

            var accountIds =
                journalEntry.Lines
                    .Select(line =>
                        line.AccountId)
                    .Distinct()
                    .ToList();

            var accounts =
                await _repository.GetAccountsAsync(
                    journalEntry.CompanyId,
                    accountIds,
                    cancellationToken);

            if (accounts.Count != accountIds.Count)
            {
                throw new ValidationException(
                    "One or more journal accounts could not be found.");
            }

            foreach (var account in accounts)
            {
                ValidateManualPostingAccount(
                    account);
            }

            journalEntry.TotalDebit =
                totalDebit;

            journalEntry.TotalCredit =
                totalCredit;
        }

        private async Task ValidateCompanyAsync(
            int companyId,
            CancellationToken cancellationToken)
        {
            var companyExists =
                await _repository.CompanyExistsAsync(
                    companyId,
                    cancellationToken);

            if (!companyExists)
            {
                throw new ValidationException(
                    "Select an existing company.");
            }
        }

        private static void
            ValidateManualPostingAccount(
                Account account)
        {
            if (!account.IsActive)
            {
                throw new ValidationException(
                    $"Account {account.Code} - " +
                    $"{account.Name} is inactive.");
            }

            if (!account.IsPostingAccount)
            {
                throw new ValidationException(
                    $"Account {account.Code} - " +
                    $"{account.Name} is a group account and cannot receive journal entries.");
            }

            if (account.IsControlAccount)
            {
                throw new ValidationException(
                    $"Account {account.Code} - " +
                    $"{account.Name} is a control account. " +
                    "It must be posted by its connected business module.");
            }
        }

        private static void ValidateEntryDate(
            DateOnly entryDate)
        {
            if (entryDate == default)
            {
                throw new ValidationException(
                    "Entry date is required.");
            }
        }

        private static decimal
            NormalizeExchangeRate(
                decimal exchangeRate)
        {
            if (exchangeRate <= 0)
            {
                throw new ValidationException(
                    "Exchange rate must be greater than zero.");
            }

            return decimal.Round(
                exchangeRate,
                8,
                MidpointRounding.AwayFromZero);
        }

        private static decimal RoundAmount(
            decimal amount)
        {
            return decimal.Round(
                amount,
                4,
                MidpointRounding.AwayFromZero);
        }

        private static string NormalizeCurrency(
            string currency)
        {
            var normalizedCurrency =
                NormalizeRequiredText(
                    currency,
                    "Currency is required.")
                .ToUpperInvariant();

            if (
                normalizedCurrency.Length != 3 ||
                !normalizedCurrency.All(
                    character =>
                        char.IsLetter(character)))
            {
                throw new ValidationException(
                    "Currency must be a valid three-letter code.");
            }

            return normalizedCurrency;
        }

        private static string NormalizeRequiredText(
            string value,
            string errorMessage)
        {
            var normalizedValue =
                value.Trim();

            if (string.IsNullOrWhiteSpace(
                    normalizedValue))
            {
                throw new ValidationException(
                    errorMessage);
            }

            return normalizedValue;
        }

        private static string? NormalizeOptionalText(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static string GenerateEntryNumber(
            string prefix,
            DateOnly entryDate)
        {
            var uniquePart =
                Guid.NewGuid()
                    .ToString("N")[..12]
                    .ToUpperInvariant();

            return
                $"{prefix}-{entryDate:yyyyMMdd}-" +
                uniquePart;
        }

        private static byte[] DecodeRowVersion(
            string rowVersion)
        {
            try
            {
                return Convert.FromBase64String(
                    rowVersion);
            }
            catch (
                FormatException)
            {
                throw new ValidationException(
                    "Row version is invalid. Reload the journal entry.");
            }
        }

        private static void EnsureRowVersionMatches(
            JournalEntry journalEntry,
            byte[] rowVersion)
        {
            if (!journalEntry.RowVersion.SequenceEqual(
                    rowVersion))
            {
                throw new DbUpdateConcurrencyException(
                    "This journal entry has changed. Reload it before continuing.");
            }
        }

        private static string
            BuildReversalDescription(
                string description)
        {
            if (string.IsNullOrWhiteSpace(
                    description))
            {
                return "Reversal";
            }

            var value =
                $"Reversal: {description.Trim()}";

            return value.Length <= 500
                ? value
                : value[..500];
        }

        private static string BuildReversalNarration(
            string entryNumber,
            string reason)
        {
            var value =
                $"Reversal of {entryNumber}: {reason}";

            return value.Length <= 500
                ? value
                : value[..500];
        }

        private static void Validate(
            object value)
        {
            Validator.ValidateObject(
                value,
                new ValidationContext(value),
                validateAllProperties: true);
        }

        private static JournalEntryListDto
            ToListResponse(
                JournalEntry journalEntry)
        {
            return new JournalEntryListDto
            {
                Id =
                    journalEntry.Id,

                CompanyId =
                    journalEntry.CompanyId,

                CompanyName =
                    journalEntry.Company?.Name ??
                    string.Empty,

                EntryNumber =
                    journalEntry.EntryNumber,

                EntryDate =
                    journalEntry.EntryDate,

                Branch =
                    journalEntry.Branch,

                Currency =
                    journalEntry.Currency,

                Narration =
                    journalEntry.Narration,

                Reference =
                    journalEntry.Reference,

                Source =
                    journalEntry.Source.ToString(),

                SourceDocumentNumber =
                    journalEntry.SourceDocumentNumber,

                Status =
                    journalEntry.Status.ToString(),

                TotalDebit =
                    journalEntry.TotalDebit,

                TotalCredit =
                    journalEntry.TotalCredit,

                CreatedDate =
                    journalEntry.CreatedDate,

                PostedDate =
                    journalEntry.PostedDate
            };
        }

        private static JournalEntryResponseDto ToResponse(
            JournalEntry journalEntry)
        {
            return new JournalEntryResponseDto
            {
                Id =
                    journalEntry.Id,

                CompanyId =
                    journalEntry.CompanyId,

                CompanyName =
                    journalEntry.Company?.Name ??
                    string.Empty,

                EntryNumber =
                    journalEntry.EntryNumber,

                EntryDate =
                    journalEntry.EntryDate,

                Branch =
                    journalEntry.Branch,

                Currency =
                    journalEntry.Currency,

                ExchangeRate =
                    journalEntry.ExchangeRate,

                Narration =
                    journalEntry.Narration,

                Reference =
                    journalEntry.Reference,

                Source =
                    journalEntry.Source.ToString(),

                SourceDocumentId =
                    journalEntry.SourceDocumentId,

                SourceDocumentNumber =
                    journalEntry.SourceDocumentNumber,

                Status =
                    journalEntry.Status.ToString(),

                TotalDebit =
                    journalEntry.TotalDebit,

                TotalCredit =
                    journalEntry.TotalCredit,

                ReversalOfJournalEntryId =
                    journalEntry
                        .ReversalOfJournalEntryId,

                ReversedByJournalEntryId =
                    journalEntry.ReversalEntries
                        .OrderBy(entry =>
                            entry.Id)
                        .Select(entry =>
                            (int?)entry.Id)
                        .FirstOrDefault(),

                CreatedDate =
                    journalEntry.CreatedDate,

                ModifiedDate =
                    journalEntry.ModifiedDate,

                PostedDate =
                    journalEntry.PostedDate,

                ReversedDate =
                    journalEntry.ReversedDate,

                RowVersion =
                    Convert.ToBase64String(
                        journalEntry.RowVersion),

                Lines =
                    journalEntry.Lines
                        .OrderBy(line =>
                            line.LineNumber)
                        .Select(line =>
                            new JournalEntryLineResponseDto
                            {
                                Id =
                                    line.Id,

                                LineNumber =
                                    line.LineNumber,

                                AccountId =
                                    line.AccountId,

                                AccountCode =
                                    line.Account?.Code ??
                                    string.Empty,

                                AccountName =
                                    line.Account?.Name ??
                                    string.Empty,

                                Description =
                                    line.Description,

                                DebitAmount =
                                    line.DebitAmount,

                                CreditAmount =
                                    line.CreditAmount,

                                BaseDebitAmount =
                                    line.BaseDebitAmount,

                                BaseCreditAmount =
                                    line.BaseCreditAmount,

                                CostCenter =
                                    line.CostCenter,

                                CounterpartyType =
                                    line.CounterpartyType,

                                CounterpartyId =
                                    line.CounterpartyId
                            })
                        .ToList()
            };
        }

        private sealed record JournalLineBuildResult(
            IReadOnlyList<JournalEntryLine> Lines,
            decimal TotalDebit,
            decimal TotalCredit);
    }
}