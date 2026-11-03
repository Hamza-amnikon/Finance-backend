using System.ComponentModel.DataAnnotations;
using AccountingService.API.DTOs;
using AccountingService.API.Models;
using AccountingService.API.Repositories;

namespace AccountingService.API.Services
{
    public class GeneralLedgerService
        : IGeneralLedgerService
    {
        private readonly IGeneralLedgerRepository
            _repository;

        public GeneralLedgerService(
            IGeneralLedgerRepository repository)
        {
            _repository = repository;
        }

        public async Task<GeneralLedgerResultDto>
            GetAsync(
                GeneralLedgerQueryDto query,
                CancellationToken cancellationToken)
        {
            Validate(query);
            ValidateDates(query);

            var account =
                await _repository.GetAccountAsync(
                    query.AccountId,
                    query.CompanyId,
                    cancellationToken);

            if (account == null)
            {
                throw new ValidationException(
                    "Select an account from the selected company.");
            }

            if (!account.IsPostingAccount)
            {
                throw new ValidationException(
                    "Select a posting account. " +
                    "Group accounts do not contain direct journal entries.");
            }

            var openingSignedBalance =
                await _repository
                    .GetOpeningBalanceAsync(
                        query,
                        cancellationToken);

            var page =
                await _repository.GetPageAsync(
                    query,
                    cancellationToken);

            var runningSignedBalance =
                openingSignedBalance +
                page.AmountBeforePage;

            var lines =
                new List<GeneralLedgerLineDto>();

            foreach (var row in page.Rows)
            {
                runningSignedBalance +=
                    row.BaseDebit -
                    row.BaseCredit;

                lines.Add(
                    new GeneralLedgerLineDto
                    {
                        JournalEntryId =
                            row.JournalEntryId,

                        JournalEntryLineId =
                            row.JournalEntryLineId,

                        LineNumber =
                            row.LineNumber,

                        EntryNumber =
                            row.EntryNumber,

                        EntryDate =
                            row.EntryDate,

                        Branch =
                            row.Branch,

                        Source =
                            row.Source.ToString(),

                        Status =
                            row.Status.ToString(),

                        SourceDocumentNumber =
                            row.SourceDocumentNumber,

                        Reference =
                            row.Reference,

                        Narration =
                            row.Narration,

                        Description =
                            row.Description,

                        TransactionCurrency =
                            row.TransactionCurrency,

                        ExchangeRate =
                            row.ExchangeRate,

                        TransactionDebit =
                            RoundAmount(
                                row.TransactionDebit),

                        TransactionCredit =
                            RoundAmount(
                                row.TransactionCredit),

                        BaseDebit =
                            RoundAmount(
                                row.BaseDebit),

                        BaseCredit =
                            RoundAmount(
                                row.BaseCredit),

                        RunningBalance =
                            RoundAmount(
                                Math.Abs(
                                    runningSignedBalance)),

                        BalanceSide =
                            GetBalanceSide(
                                runningSignedBalance,
                                account.NormalBalance),

                        CostCenter =
                            row.CostCenter,

                        CounterpartyType =
                            row.CounterpartyType,

                        CounterpartyId =
                            row.CounterpartyId
                    });
            }

            var closingSignedBalance =
                openingSignedBalance +
                page.PeriodDebit -
                page.PeriodCredit;

            return new GeneralLedgerResultDto
            {
                CompanyId =
                    account.CompanyId,

                CompanyName =
                    account.Company.Name,

                BaseCurrency =
                    account.Company.Currency,

                AccountId =
                    account.Id,

                AccountCode =
                    account.Code,

                AccountName =
                    account.Name,

                AccountType =
                    account.Type.ToString(),

                NormalBalance =
                    account.NormalBalance
                        .ToString(),

                FromDate =
                    query.FromDate,

                ToDate =
                    query.ToDate,

                Branch =
                    NormalizeOptionalText(
                        query.Branch),

                CostCenter =
                    NormalizeOptionalText(
                        query.CostCenter),

                OpeningBalance =
                    RoundAmount(
                        Math.Abs(
                            openingSignedBalance)),

                OpeningBalanceSide =
                    GetBalanceSide(
                        openingSignedBalance,
                        account.NormalBalance),

                PeriodDebit =
                    RoundAmount(
                        page.PeriodDebit),

                PeriodCredit =
                    RoundAmount(
                        page.PeriodCredit),

                ClosingBalance =
                    RoundAmount(
                        Math.Abs(
                            closingSignedBalance)),

                ClosingBalanceSide =
                    GetBalanceSide(
                        closingSignedBalance,
                        account.NormalBalance),

                Data =
                    lines,

                Total =
                    page.Total,

                Page =
                    query.Page,

                PageSize =
                    query.PageSize
            };
        }

        private static void ValidateDates(
            GeneralLedgerQueryDto query)
        {
            if (
                query.FromDate ==
                default)
            {
                throw new ValidationException(
                    "From date is required.");
            }

            if (
                query.ToDate ==
                default)
            {
                throw new ValidationException(
                    "To date is required.");
            }

            if (
                query.FromDate >
                query.ToDate)
            {
                throw new ValidationException(
                    "From date cannot be later than to date.");
            }
        }

        private static string GetBalanceSide(
            decimal signedBalance,
            AccountNormalBalance normalBalance)
        {
            if (signedBalance > 0)
            {
                return "Debit";
            }

            if (signedBalance < 0)
            {
                return "Credit";
            }

            return normalBalance.ToString();
        }

        private static decimal RoundAmount(
            decimal amount)
        {
            return decimal.Round(
                amount,
                4,
                MidpointRounding.AwayFromZero);
        }

        private static string?
            NormalizeOptionalText(
                string? value)
        {
            if (string.IsNullOrWhiteSpace(
                value))
            {
                return null;
            }

            return value.Trim();
        }

        private static void Validate(
            object value)
        {
            Validator.ValidateObject(
                value,
                new ValidationContext(value),
                validateAllProperties: true);
        }
    }
}