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
    public static class BankReconciliationHelper
    {
        // Storage limit for decimal(18,4).
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
            byte[] current,
            byte[] supplied)
        {
            if (supplied == null || supplied.Length != 8)
            {
                throw new ValidationException(
                    "A valid row version is required.");
            }

            if (!current.SequenceEqual(supplied))
            {
                throw new DbUpdateConcurrencyException(
                    "This reconciliation has changed. Refresh and try again.");
            }
        }

        public static void CheckDraft(
            BankReconciliation reconciliation)
        {
            if (reconciliation.Status != "Draft")
            {
                throw new InvalidOperationException(
                    "Completed reconciliations cannot be changed.");
            }
        }

        public static void ValidateAmount(
            decimal amount,
            int decimalPlaces,
            string fieldName,
            bool requirePositive = false)
        {
            if (decimalPlaces < 0 || decimalPlaces > 4)
            {
                throw new ValidationException(
                    "The account has unsupported currency precision.");
            }

            if (
                amount < -MaximumAmount ||
                amount > MaximumAmount ||
                (requirePositive && amount <= 0))
            {
                throw new ValidationException(
                    fieldName + " is outside the supported amount range.");
            }

            if (decimal.Round(amount, decimalPlaces) != amount)
            {
                throw new ValidationException(
                    fieldName + " supports at most " +
                    decimalPlaces + " decimal places.");
            }
        }

        public static void ValidateStatement(
            CreateBankReconciliationDto dto,
            BankAccount account)
        {
            Validate(dto);

            if (!account.OpeningBalanceDate.HasValue)
            {
                throw new ValidationException(
                    "Configure the bank account opening balance first.");
            }

            if (dto.PeriodStart < account.OpeningBalanceDate.Value)
            {
                throw new ValidationException(
                    "The statement cannot start before the account opening date.");
            }

            ValidateAmount(
                dto.StatementOpeningBalance,
                account.CurrencyDecimalPlaces,
                "Statement opening balance");

            ValidateAmount(
                dto.StatementClosingBalance,
                account.CurrencyDecimalPlaces,
                "Statement closing balance");

            if (dto.Lines == null || dto.Lines.Count > 2000)
            {
                throw new ValidationException(
                    "Provide a statement line list containing at most 2,000 entries.");
            }

            var calculatedClosingBalance = dto.StatementOpeningBalance;

            foreach (var line in dto.Lines)
            {
                if (line == null)
                {
                    throw new ValidationException(
                        "Statement lines cannot contain null entries.");
                }

                Validate(line);

                if (
                    line.TransactionDate < dto.PeriodStart ||
                    line.TransactionDate > dto.PeriodEnd)
                {
                    throw new ValidationException(
                        "Every statement entry must fall within the statement period.");
                }

                ValidateAmount(
                    line.Amount,
                    account.CurrencyDecimalPlaces,
                    "Statement entry amount",
                    requirePositive: true);

                calculatedClosingBalance +=
                    line.Direction == BankTransactionDirection.MoneyIn
                        ? line.Amount
                        : -line.Amount;
            }

            if (calculatedClosingBalance != dto.StatementClosingBalance)
            {
                throw new ValidationException(
                    "Statement opening balance plus money in minus money out " +
                    "must equal the statement closing balance.");
            }
        }

        public static void ValidateMatch(
            BankReconciliation reconciliation,
            BankStatementLine line,
            BankTransaction transaction)
        {
            if (transaction.Status != "Posted")
            {
                throw new ValidationException(
                    "Only posted bank transactions can be matched.");
            }

            if (
                transaction.BankAccountId != reconciliation.BankAccountId ||
                transaction.CompanyId != reconciliation.CompanyId)
            {
                throw new ValidationException(
                    "The transaction belongs to a different account or company.");
            }

            if (
                transaction.Currency != reconciliation.Currency ||
                transaction.CurrencyDecimalPlaces !=
                    reconciliation.CurrencyDecimalPlaces)
            {
                throw new ValidationException(
                    "The transaction currency does not match the statement.");
            }

            if (
                transaction.Direction != line.Direction ||
                transaction.Amount != line.Amount)
            {
                throw new ValidationException(
                    "The transaction direction and amount must match the statement entry.");
            }

            if (
                transaction.TransactionDate <
                    reconciliation.BankAccount.OpeningBalanceDate ||
                transaction.TransactionDate > reconciliation.PeriodEnd)
            {
                throw new ValidationException(
                    "The transaction date is outside the supported reconciliation range.");
            }

            // Earlier book entries may clear in this statement period.
            // Their date does not have to equal the bank statement date.
        }

        public static string CreateFingerprint(
            CreateBankReconciliationDto dto)
        {
            var payload = JsonSerializer.Serialize(new
            {
                dto.BankAccountId,

                PeriodStart = dto.PeriodStart.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture),

                PeriodEnd = dto.PeriodEnd.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture),

                OpeningBalance = dto.StatementOpeningBalance.ToString(
                    "G29",
                    CultureInfo.InvariantCulture),

                ClosingBalance = dto.StatementClosingBalance.ToString(
                    "G29",
                    CultureInfo.InvariantCulture),

                Notes = dto.Notes?.Trim() ?? "",

                Lines = dto.Lines.Select(line => new
                {
                    Date = line.TransactionDate.ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture),

                    Direction = (int)line.Direction,

                    Amount = line.Amount.ToString(
                        "G29",
                        CultureInfo.InvariantCulture),

                    Reference = line.Reference?.Trim() ?? "",
                    Description = line.Description?.Trim() ?? ""
                }).ToArray()
            });

            var bytes = Encoding.UTF8.GetBytes(payload);

            return Convert.ToHexString(
                SHA256.HashData(bytes));
        }
    }
}