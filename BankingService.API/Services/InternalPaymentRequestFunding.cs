using System.Data;
using System.Globalization;
using BankingService.API.DTOs;
using BankingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Services
{
    public sealed partial class InternalPaymentRequestService
    {
        public async Task<object> GetPrintableAsync(
            int id,
            InternalPaymentRequestScope scope,
            string displayName,
            CancellationToken cancellationToken)
        {
            var request = await GetAsync(id, scope, cancellationToken);
            Require(scope.CanExport(request.RequestingCompanyId) || scope.CanExport(request.PayingCompanyId),
                403, "Export permission for a participating company is required to print this request.");
            var companies = await database.Companies.AsNoTracking()
                .Where(row => row.Id == request.RequestingCompanyId || row.Id == request.PayingCompanyId)
                .ToDictionaryAsync(row => row.Id, row => row.Name, cancellationToken);
            var bank = await database.BankAccounts.AsNoTracking()
                .Where(row => row.Id == request.ReceivingBankAccountId)
                .Select(row => new { row.Name, row.BankName, row.AccountNumber, row.Currency })
                .SingleAsync(cancellationToken);
            var profiles = await database.Database.SqlQuery<InternalRequestCompanyProfile>($"""
                SELECT AccountingCompanyId, CompanyName, Address, CountryCode,
                    CompanyRegistrationNumber, TaxRegistrationNumber, Phone, Email,
                    BankAccountNumber, BankIban, BankSwiftCode
                FROM amnikon.Branches
                WHERE AccountingCompanyId = {request.RequestingCompanyId}
                    OR AccountingCompanyId = {request.PayingCompanyId}
                """).ToListAsync(cancellationToken);
            Require(profiles.Count(profile => profile.AccountingCompanyId == request.RequestingCompanyId) == 1,
                409, "Configure one invoice company profile for the requesting company before printing.");
            Require(profiles.Count(profile => profile.AccountingCompanyId == request.PayingCompanyId) <= 1,
                409, "The paying company has multiple invoice profiles. Correct its company mapping before printing.");
            var issuerProfile = profiles.Single(profile => profile.AccountingCompanyId == request.RequestingCompanyId);
            var recipientProfile = profiles.SingleOrDefault(profile => profile.AccountingCompanyId == request.PayingCompanyId);
            var sameBankAccount = !string.IsNullOrWhiteSpace(bank.AccountNumber) &&
                string.Equals(bank.AccountNumber.Trim(), issuerProfile.BankAccountNumber.Trim(), StringComparison.OrdinalIgnoreCase);
            return new
            {
                displayName,
                request,
                issuer = new
                {
                    companyName = string.IsNullOrWhiteSpace(issuerProfile.CompanyName)
                        ? companies.GetValueOrDefault(request.RequestingCompanyId) : issuerProfile.CompanyName,
                    issuerProfile.Address,
                    issuerProfile.CountryCode,
                    issuerProfile.CompanyRegistrationNumber,
                    issuerProfile.TaxRegistrationNumber,
                    issuerProfile.Phone,
                    issuerProfile.Email,
                    bankAccountName = bank.Name,
                    bank.BankName,
                    bankAccountNumber = bank.AccountNumber,
                    bankIban = sameBankAccount ? issuerProfile.BankIban : "",
                    bankSwiftCode = sameBankAccount ? issuerProfile.BankSwiftCode : ""
                },
                recipient = new
                {
                    companyName = string.IsNullOrWhiteSpace(recipientProfile?.CompanyName)
                        ? companies.GetValueOrDefault(request.PayingCompanyId) : recipientProfile.CompanyName,
                    address = recipientProfile?.Address ?? ""
                },
                requestingCompany = companies.GetValueOrDefault(request.RequestingCompanyId),
                payingCompany = companies.GetValueOrDefault(request.PayingCompanyId),
                receivingBank = bank,
                transfers = await GetLinkedTransfersAsync(id, cancellationToken)
            };
        }

        public async Task<object> GetTransferCandidatesAsync(
            int id,
            int page,
            InternalPaymentRequestScope scope,
            InternalPaymentRequestScope transferScope,
            CancellationToken cancellationToken)
        {
            Require(page > 0 && page <= 100000, 400, "Choose a valid transfer page.");
            var request = await GetAsync(id, scope, cancellationToken);
            RequireLinkAccess(request, scope, transferScope);
            var query = database.BankTransfers.AsNoTracking().Where(transfer =>
                transfer.Status == BankTransferStatus.Completed &&
                transfer.SourceCompanyId == request.PayingCompanyId &&
                transfer.DestinationCompanyId == request.RequestingCompanyId &&
                transfer.DestinationBankAccountId == request.ReceivingBankAccountId &&
                transfer.DestinationCurrency == request.Currency &&
                transfer.DestinationAmount > 0m && transfer.DestinationAmount <= request.RemainingAmount &&
                transfer.SourceTransactionId != null && transfer.DestinationTransactionId != null &&
                !database.InternalPaymentRequestTransfers.Any(link => link.BankTransferId == transfer.Id));
            var total = await query.CountAsync(cancellationToken);
            var rows = await query.OrderByDescending(row => row.Id)
                .Skip((page - 1) * 25).Take(25).ToListAsync(cancellationToken);
            return new { data = rows.Select(TransferSummary), total, page, pageSize = 25 };
        }

        public async Task<object> LinkTransferAsync(
            int id,
            InternalPaymentRequestTransferDto dto,
            InternalPaymentRequestScope scope,
            InternalPaymentRequestScope transferScope,
            CancellationToken cancellationToken)
        {
            // Lock the request and transfer so simultaneous links cannot overfund a request
            // or allocate one transfer twice. Neither bank-transfer row is changed.
            await using var transaction = await database.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var request = await database.InternalPaymentRequests.FromSqlInterpolated($"""
                SELECT * FROM banking.InternalPaymentRequests WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}
                """).SingleOrDefaultAsync(cancellationToken);
            Require(request != null && (scope.CanView(request.RequestingCompanyId) || scope.CanView(request.PayingCompanyId)),
                404, "The request was not found.");
            RequireLinkAccess(request!, scope, transferScope);
            var transfer = await database.BankTransfers.FromSqlInterpolated($"""
                SELECT * FROM banking.BankTransfers WITH (UPDLOCK, HOLDLOCK) WHERE Id = {dto.BankTransferId}
                """).SingleOrDefaultAsync(cancellationToken);
            Require(transfer != null, 404, "The bank transfer was not found.");
            ValidateTransfer(request!, transfer!);
            var existing = await database.InternalPaymentRequestTransfers.AsNoTracking()
                .SingleOrDefaultAsync(row => row.BankTransferId == dto.BankTransferId, cancellationToken);
            if (existing != null)
            {
                Require(existing.InternalPaymentRequestId == id, 409, "This transfer is already linked to another request.");
                await transaction.CommitAsync(cancellationToken);
                return await GetDetailAsync(id, scope, cancellationToken);
            }
            Require(request!.RowVersion.SequenceEqual(dto.RowVersion), 409, "The request changed. Reload it before linking a transfer.");
            await PopulateFundingAsync([request], cancellationToken);
            Require(transfer!.DestinationAmount <= request.RemainingAmount, 409,
                "This transfer exceeds the request's remaining amount.");
            database.InternalPaymentRequestTransfers.Add(new InternalPaymentRequestTransfer
            {
                InternalPaymentRequestId = id,
                BankTransferId = transfer.Id,
                LinkedDate = DateTime.UtcNow,
                LinkedBy = scope.ActorId
            });
            repository.SetVersion(request, dto.RowVersion);
            request.ModifiedDate = DateTime.UtcNow;
            await repository.SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await GetDetailAsync(id, scope, cancellationToken);
        }

        public static void RequireLinkAccess(
            InternalPaymentRequest request,
            InternalPaymentRequestScope scope,
            InternalPaymentRequestScope transferScope)
        {
            Require(scope.CanEdit(request.PayingCompanyId), 403,
                "Edit permission for the paying company is required to link transfers.");
            Require(transferScope.CanView(request.PayingCompanyId), 403,
                "Bank Transfers View permission for the paying company is required.");
            Require(request.Status == InternalPaymentRequestStatus.Approved, 409,
                "Only an approved request can be linked to transfers.");
        }

        public static void ValidateTransfer(InternalPaymentRequest request, BankTransfer transfer)
        {
            Require(transfer.Status == BankTransferStatus.Completed &&
                transfer.SourceTransactionId.HasValue && transfer.DestinationTransactionId.HasValue,
                409, "Only a completed transfer with posted bank transactions can be linked.");
            Require(transfer.SourceCompanyId == request.PayingCompanyId &&
                transfer.DestinationCompanyId == request.RequestingCompanyId &&
                transfer.DestinationBankAccountId == request.ReceivingBankAccountId &&
                transfer.DestinationCurrency == request.Currency,
                400, "The transfer must match the paying company, requesting company, receiving bank and request currency.");
            Require(transfer.DestinationAmount is > 0m && request.CurrencyDecimalPlaces is >= 0 and <= 4 &&
                decimal.Round(transfer.DestinationAmount.Value, request.CurrencyDecimalPlaces) == transfer.DestinationAmount,
                400, "The transfer has an invalid received amount.");
        }

        private async Task PopulateFundingAsync(
            IReadOnlyCollection<InternalPaymentRequest> requests,
            CancellationToken cancellationToken)
        {
            var ids = requests.Select(row => row.Id).ToArray();
            var totals = await (from link in database.InternalPaymentRequestTransfers.AsNoTracking()
                join transfer in database.BankTransfers.AsNoTracking() on link.BankTransferId equals transfer.Id
                where ids.Contains(link.InternalPaymentRequestId) && transfer.Status == BankTransferStatus.Completed
                group transfer by link.InternalPaymentRequestId into transfers
                select new { Id = transfers.Key, Amount = transfers.Sum(row => row.DestinationAmount ?? 0m) })
                .ToDictionaryAsync(row => row.Id, row => row.Amount, cancellationToken);
            foreach (var request in requests)
            {
                request.ReceivedAmount = totals.GetValueOrDefault(request.Id);
            }
        }

        private async Task<object[]> GetLinkedTransfersAsync(int id, CancellationToken cancellationToken)
        {
            var rows = await (from link in database.InternalPaymentRequestTransfers.AsNoTracking()
                join transfer in database.BankTransfers.AsNoTracking() on link.BankTransferId equals transfer.Id
                where link.InternalPaymentRequestId == id
                orderby link.Id
                select transfer).ToListAsync(cancellationToken);
            return rows.Select(TransferSummary).ToArray();
        }

        private static object TransferSummary(BankTransfer transfer)
        {
            return new
            {
                transfer.Id,
                transfer.TransferDate,
                transfer.ReceivedDate,
                transfer.Reference,
                transfer.SourceCurrency,
                sourceAmount = transfer.SourceAmount.ToString(CultureInfo.InvariantCulture),
                transfer.DestinationCurrency,
                receivedAmount = transfer.DestinationAmount?.ToString(CultureInfo.InvariantCulture),
                status = transfer.Status.ToString()
            };
        }
    }
}
