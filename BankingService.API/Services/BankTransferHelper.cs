using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BankingService.API.DTOs;
using BankingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Services
{
    public static class BankTransferHelper
    {
        // Maximum value supported by decimal(18,4).
        private const decimal MaximumAmount =
            99999999999999.9999m;

        public static void Validate(
            object dto)
        {
            Validator.ValidateObject(
                dto,
                new ValidationContext(dto),
                validateAllProperties: true);
        }

        public static void CheckVersion(
            BankTransfer transfer,
            byte[] rowVersion)
        {
            if (
                rowVersion == null ||
                rowVersion.Length != 8)
            {
                throw new ValidationException(
                    "A valid row version is required.");
            }

            if (!transfer.RowVersion.SequenceEqual(rowVersion))
            {
                throw new DbUpdateConcurrencyException(
                    "This transfer has changed. Refresh it and try again.");
            }
        }

        public static void CheckStatus(
            BankTransfer transfer,
            BankTransferStatus expectedStatus)
        {
            if (transfer.Status != expectedStatus)
            {
                throw new InvalidOperationException(
                    $"This action requires a {expectedStatus} transfer.");
            }
        }

        public static void ValidateAccount(
            BankAccount account,
            bool requireActive)
        {
            if (requireActive && !account.IsActive)
            {
                throw new ValidationException(
                    $"Bank account '{account.Name}' is inactive.");
            }

            if (!account.OpeningBalanceDate.HasValue)
            {
                throw new ValidationException(
                    $"Configure the opening balance for '{account.Name}' first.");
            }

            if (
                string.IsNullOrWhiteSpace(account.Currency) ||
                account.Currency.Length != 3)
            {
                throw new ValidationException(
                    $"Bank account '{account.Name}' has an invalid currency.");
            }

            if (
                account.CurrencyDecimalPlaces < 0 ||
                account.CurrencyDecimalPlaces > 4)
            {
                throw new ValidationException(
                    $"Bank account '{account.Name}' has unsupported currency precision.");
            }
        }

        public static void ValidatePostingDate(
            BankAccount account,
            DateOnly date)
        {
            if (date == default)
            {
                throw new ValidationException(
                    "Transaction date is required.");
            }

            if (!account.OpeningBalanceDate.HasValue)
            {
                throw new ValidationException(
                    $"Configure the opening balance for '{account.Name}' first.");
            }

            if (date < account.OpeningBalanceDate.Value)
            {
                throw new ValidationException(
                    $"Transaction date cannot be before the opening balance date for '{account.Name}'.");
            }
        }

        public static void ValidateAmount(
            decimal amount,
            int decimalPlaces,
            string fieldName,
            bool allowZero = false)
        {
            if (decimalPlaces < 0 || decimalPlaces > 4)
            {
                throw new ValidationException(
                    "Currency precision must be between 0 and 4.");
            }

            if (
                amount < 0 ||
                (!allowZero && amount == 0) ||
                amount > MaximumAmount)
            {
                throw new ValidationException(
                    allowZero
                        ? $"{fieldName} must be zero or a positive amount within the supported limit."
                        : $"{fieldName} must be a positive amount within the supported limit.");
            }

            if (decimal.Round(amount, decimalPlaces) != amount)
            {
                throw new ValidationException(
                    $"{fieldName} supports at most {decimalPlaces} decimal places.");
            }
        }

        public static void ValidateSnapshot(
            BankTransfer transfer)
        {
            var source = transfer.SourceBankAccount;
            var destination = transfer.DestinationBankAccount;

            if (
                source.Id != transfer.SourceBankAccountId ||
                source.CompanyId != transfer.SourceCompanyId ||
                source.Currency != transfer.SourceCurrency ||
                source.CurrencyDecimalPlaces !=
                    transfer.SourceCurrencyDecimalPlaces)
            {
                throw new ValidationException(
                    "The source account no longer matches the saved transfer details.");
            }

            if (
                destination.Id != transfer.DestinationBankAccountId ||
                destination.CompanyId != transfer.DestinationCompanyId ||
                destination.Currency != transfer.DestinationCurrency ||
                destination.CurrencyDecimalPlaces !=
                    transfer.DestinationCurrencyDecimalPlaces)
            {
                throw new ValidationException(
                    "The destination account no longer matches the saved transfer details.");
            }

            if (source.Id == destination.Id)
            {
                throw new ValidationException(
                    "Source and destination accounts must be different.");
            }
        }

        public static decimal CalculateEffectiveRate(
            decimal sourceAmount,
            decimal destinationAmount)
        {
            if (sourceAmount <= 0 || destinationAmount <= 0)
            {
                throw new ValidationException(
                    "Both transfer amounts must be greater than zero.");
            }

            decimal rate;

            try
            {
                rate = decimal.Round(
                    destinationAmount / sourceAmount,
                    12,
                    MidpointRounding.AwayFromZero);
            }
            catch (OverflowException)
            {
                throw new ValidationException(
                    "The effective exchange rate exceeds the supported limit.");
            }

            // decimal(28,12) supports 16 integer digits.
            if (
                rate <= 0 ||
                rate >= 10000000000000000m)
            {
                throw new ValidationException(
                    "The effective exchange rate is outside the supported range.");
            }

            return rate;
        }

        public static string CreateFingerprint(
            CreateBankTransferDto dto)
        {
            var payload = JsonSerializer.Serialize(new
            {
                dto.SourceBankAccountId,
                dto.DestinationBankAccountId,

                SourceAmount = dto.SourceAmount.ToString(
                    "G29",
                    CultureInfo.InvariantCulture),

                SourceFeeAmount = dto.SourceFeeAmount.ToString(
                    "G29",
                    CultureInfo.InvariantCulture),

                TransferDate = dto.TransferDate.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture),

                Reference = dto.Reference?.Trim() ?? "",
                Notes = dto.Notes?.Trim() ?? ""
            });

            var bytes = Encoding.UTF8.GetBytes(payload);
            var hash = SHA256.HashData(bytes);

            return Convert.ToHexString(hash);
        }
    }
}