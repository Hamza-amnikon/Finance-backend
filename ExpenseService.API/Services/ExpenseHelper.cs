using System.ComponentModel.DataAnnotations;
using ExpenseService.API.DTOs;

namespace ExpenseService.API.Services
{
    public static class ExpenseHelper
    {
        public static decimal? Convert(
            decimal amount,
            string currency,
            string displayCurrency,
            decimal rate,
            string baseCurrency,
            string quoteCurrency)
        {
            if (currency == displayCurrency)
            {
                return amount;
            }

            if (rate <= 0)
            {
                return null;
            }

            if (currency == baseCurrency && displayCurrency == quoteCurrency)
            {
                return amount * rate;
            }

            if (currency == quoteCurrency && displayCurrency == baseCurrency)
            {
                return amount / rate;
            }

            return null;
        }

        public static decimal? Round(
            decimal? amount,
            int decimalPlaces)
        {
            return amount.HasValue
                ? decimal.Round(
                    amount.Value,
                    decimalPlaces,
                    MidpointRounding.AwayFromZero)
                : null;
        }

        public static void Validate(
            ExpenseDto dto)
        {
            Validator.ValidateObject(
                dto,
                new ValidationContext(dto),
                true);

            if (dto.RequestId == Guid.Empty)
            {
                throw new ValidationException("Request ID is required.");
            }

            if (dto.Date == default)
            {
                throw new ValidationException(
                    "Expense date is required.");
            }

            if (dto.Currency != dto.RateBaseCurrency && dto.Currency != dto.RateQuoteCurrency)
            {
                throw new ValidationException(
                    "The saved rate pair must include the expense currency.");
            }

            if (dto.Status == "Recorded" && dto.BankAccountId is null)
            {
                throw new ValidationException(
                    "Select the bank account used to pay this expense.");
            }
        }

        public static void CheckVersion(
            byte[] current,
            byte[]? supplied)
        {
            if (supplied is not { Length: 8 })
            {
                throw new ValidationException(
                    "Refresh the expense before trying again.");
            }

            if (!current.SequenceEqual(supplied))
            {
                throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException();
            }
        }
    }
}
