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
    public static class BankTransactionHelper
    {
        public const string LockedMessage =
            "Only draft bank transactions can be changed.";

        public static void Validate(
            object value)
        {
            Validator.ValidateObject(
                value,
                new ValidationContext(value),
                validateAllProperties: true);
        }

        public static void ValidateAccount(
            BankAccount account,
            DateOnly transactionDate,
            decimal amount)
        {
            if (!account.IsActive)
            {
                throw new ValidationException(
                    "Select an active bank account.");
            }

            if (!account.OpeningBalanceDate.HasValue)
            {
                throw new ValidationException(
                    "Configure the account opening balance first.");
            }

            if (transactionDate < account.OpeningBalanceDate.Value)
            {
                throw new ValidationException(
                    "Transaction date cannot be earlier than the account opening date.");
            }

            if (
                account.CurrencyDecimalPlaces < 0 ||
                account.CurrencyDecimalPlaces > 4)
            {
                throw new ValidationException(
                    "The account has unsupported currency precision.");
            }

            const decimal maximum = 99999999999999.9999m;

            if (amount <= 0 || amount > maximum)
            {
                throw new ValidationException(
                    "Enter a positive amount within the supported range.");
            }

            if (
                decimal.Round(
                    amount,
                    account.CurrencyDecimalPlaces) != amount)
            {
                throw new ValidationException(
                    $"Amount supports up to {account.CurrencyDecimalPlaces} decimal places for this account.");
            }
        }

        public static void CheckVersion(
            byte[] currentVersion,
            byte[]? suppliedVersion)
        {
            if (
                suppliedVersion == null ||
                suppliedVersion.Length != 8)
            {
                throw new ValidationException(
                    "A valid transaction version is required.");
            }

            if (!currentVersion.SequenceEqual(suppliedVersion))
            {
                throw new DbUpdateConcurrencyException(
                    "This transaction has changed. Reload it before saving.");
            }
        }

        public static void CheckDraft(
            BankTransaction transaction)
        {
            if (transaction.Status != "Draft")
            {
                throw new InvalidOperationException(
                    LockedMessage);
            }
        }

        public static string CreateFingerprint(
            CreateBankTransactionDto dto)
        {
            var payload = new
            {
                dto.BankAccountId,
                TransactionDate = dto.TransactionDate.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture),
                Direction = (int)dto.Direction,
                Amount = dto.Amount.ToString(
                    "G29",
                    CultureInfo.InvariantCulture),
                Reference = dto.Reference?.Trim() ?? "",
                Description = dto.Description?.Trim() ?? "",
                Notes = dto.Notes?.Trim() ?? "",
            };

            var json = JsonSerializer.Serialize(payload);

            var bytes = Encoding.UTF8.GetBytes(json);

            var hash = SHA256.HashData(bytes);

            return Convert.ToHexString(hash);
        }
    }
}