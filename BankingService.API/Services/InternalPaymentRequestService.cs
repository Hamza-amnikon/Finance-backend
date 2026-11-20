using BankingService.API.Data;
using BankingService.API.DTOs;
using BankingService.API.Models;
using BankingService.API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BankingService.API.Services
{
    public sealed partial class InternalPaymentRequestService(
        BankingDbContext database,
        InternalPaymentRequestRepository repository)
    {
        public async Task<object> GetPagedAsync(InternalPaymentRequestScope scope, int page, CancellationToken cancellationToken, int? companyId = null, string? status = null, string? search = null)
        {
            var query = repository.Query();
            if (!scope.IsAdmin)
            {
                query = query.Where(row => scope.CompanyIds.Contains(row.RequestingCompanyId) ||
                    scope.CompanyIds.Contains(row.PayingCompanyId));
            }
            if (companyId.HasValue)
            {
                Require(scope.CanView(companyId.Value), 403, "You cannot view requests for this company.");
                query = query.Where(row => row.RequestingCompanyId == companyId || row.PayingCompanyId == companyId);
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(row => row.Status == status);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var text = search.Trim();
                if (int.TryParse(text, out var requestNumber))
                {
                    query = query.Where(row => row.Id == requestNumber || row.Lines.Any(line => line.Purpose.Contains(text)));
                }
                else
                {
                    query = query.Where(row => row.Notes.Contains(text) || row.Lines.Any(line => line.Purpose.Contains(text)));
                }
            }
            var total = await query.CountAsync(cancellationToken);
            var data = await query.OrderByDescending(row => row.Id).Skip((page - 1) * 25).Take(25)
                .ToListAsync(cancellationToken);
            await PopulateFundingAsync(data, cancellationToken);
            return new { data, total, page, pageSize = 25 };
        }

        public async Task<object> GetSetupAsync(
            InternalPaymentRequestScope scope,
            string displayName,
            CancellationToken cancellationToken)
        {
            // Counterparty names are selectable; bank details remain scoped to View access.
            var hasAccess = scope.IsAdmin || scope.CompanyIds.Length > 0;
            var companies = await database.Companies.AsNoTracking()
                .Where(row => hasAccess)
                .OrderBy(row => row.Name)
                .Select(row => new { row.Id, row.Name, row.Currency })
                .ToListAsync(cancellationToken);
            var receivingAccounts = await database.BankAccounts.AsNoTracking()
                .Where(row => row.IsActive && (scope.IsAdmin || scope.CompanyIds.Contains(row.CompanyId)))
                .OrderBy(row => row.Name)
                .Select(row => new
                {
                    row.Id,
                    row.CompanyId,
                    row.Name,
                    row.BankName,
                    row.LastFour,
                    row.Currency,
                    row.CurrencyDecimalPlaces
                }).ToListAsync(cancellationToken);
            return new
            {
                displayName,
                companies,
                receivingAccounts,
                statuses = new[]
                {
                    InternalPaymentRequestStatus.Draft,
                    InternalPaymentRequestStatus.Submitted,
                    InternalPaymentRequestStatus.Approved,
                    InternalPaymentRequestStatus.Rejected,
                    InternalPaymentRequestStatus.Cancelled
                },
                scope.IsAdmin,
                scope.CompanyIds,
                scope.CreatableCompanyIds,
                scope.EditableCompanyIds,
                scope.ApprovableCompanyIds,
                scope.ExportableCompanyIds,
                scope.CancellableCompanyIds,
                scope.ActorId
            };
        }

        public async Task<InternalPaymentRequest> GetAsync(int id, InternalPaymentRequestScope scope, CancellationToken cancellationToken)
        {
            var request = await repository.GetAsync(id, cancellationToken);
            if (request == null || (!scope.CanView(request.RequestingCompanyId) && !scope.CanView(request.PayingCompanyId)))
            {
                throw new InternalPaymentRequestException(404, "The request was not found.");
            }
            await PopulateFundingAsync([request], cancellationToken);
            return request;
        }

        public async Task<object> GetDetailAsync(int id, InternalPaymentRequestScope scope, CancellationToken cancellationToken)
        {
            var request = await GetAsync(id, scope, cancellationToken);
            var receivingBank = await database.BankAccounts.AsNoTracking()
                .Where(row => row.Id == request.ReceivingBankAccountId)
                .Select(row => new { row.Name, row.BankName, row.LastFour, row.Currency })
                .SingleAsync(cancellationToken);
            var transfers = await GetLinkedTransfersAsync(id, cancellationToken);
            return new { request, receivingBank, transfers };
        }

        public async Task<InternalPaymentRequest> CreateAsync(
            InternalPaymentRequestDto dto,
            InternalPaymentRequestScope scope,
            CancellationToken cancellationToken)
        {
            Require(scope.CanCreate(dto.RequestingCompanyId), 403, "You cannot create requests for this company.");
            Require(dto.RequestId != Guid.Empty, 400, "Include a unique request ID.");
            var bank = await ValidateAsync(dto, cancellationToken);
            var request = new InternalPaymentRequest
            {
                RequestId = dto.RequestId,
                CreatedBy = scope.ActorId
            };
            Apply(request, dto, bank);
            repository.Add(request);
            await repository.SaveAsync(cancellationToken);
            return request;
        }

        public async Task<InternalPaymentRequest> UpdateAsync(
            int id,
            InternalPaymentRequestDto dto,
            InternalPaymentRequestScope scope,
            CancellationToken cancellationToken)
        {
            var request = await GetAsync(id, scope, cancellationToken);
            Require(scope.IsCreator(request) && scope.CanEdit(request.RequestingCompanyId), 403, "Only the creator with Edit permission can edit this draft.");
            Require(request.Status == InternalPaymentRequestStatus.Draft, 409, "Only a draft request can be edited.");
            Require(dto.RequestingCompanyId == request.RequestingCompanyId && dto.RequestId == request.RequestId,
                400, "The requesting company and request ID cannot change.");
            Require(dto.RowVersion is { Length: 8 }, 400, "Reload the request before editing.");
            var bank = await ValidateAsync(dto, cancellationToken);
            repository.SetVersion(request, dto.RowVersion!);
            Apply(request, dto, bank);
            request.ModifiedDate = DateTime.UtcNow;
            await repository.SaveAsync(cancellationToken);
            return request;
        }

        public async Task<InternalPaymentRequest> ChangeStatusAsync(
            int id,
            string action,
            InternalPaymentRequestActionDto dto,
            InternalPaymentRequestScope scope,
            CancellationToken cancellationToken)
        {
            var request = await GetAsync(id, scope, cancellationToken);
            var next = GetNextStatus(request, action, dto.Note, scope);
            repository.SetVersion(request, dto.RowVersion);
            request.Status = next;
            request.ModifiedDate = DateTime.UtcNow;
            if (action == "submit")
            {
                var bank = await database.BankAccounts.AsNoTracking()
                    .SingleAsync(row => row.Id == request.ReceivingBankAccountId, cancellationToken);
                Require(bank.IsActive && bank.Currency == request.Currency, 409,
                    "The receiving bank account is inactive or its currency changed. Update the draft before submitting.");
                request.SubmittedDate = DateTime.UtcNow;
                request.SubmittedBy = scope.ActorId;
                request.SubmissionNote = (dto.Note ?? "").Trim();
            }
            if (action == "cancel")
            {
                request.CancelledBy = scope.ActorId;
                request.CancelledDate = DateTime.UtcNow;
                request.CancellationNote = (dto.Note ?? "").Trim();
            }
            if (action is "approve" or "reject")
            {
                request.DecidedBy = scope.ActorId;
                request.DecidedDate = DateTime.UtcNow;
                request.DecisionNote = (dto.Note ?? "").Trim();
            }
            await repository.SaveAsync(cancellationToken);
            return request;
        }

        public static string GetNextStatus(
            InternalPaymentRequest request,
            string action,
            string note,
            InternalPaymentRequestScope scope)
        {
            switch (action)
            {
                case "submit":
                    Require(scope.IsCreator(request) && scope.CanEdit(request.RequestingCompanyId), 403, "Only the creator with Edit permission can submit this request.");
                    Require(request.Status == InternalPaymentRequestStatus.Draft, 409, "Only a draft can be submitted.");
                    return InternalPaymentRequestStatus.Submitted;
                case "approve":
                case "reject":
                    Require(!scope.IsCreator(request), 403, "You cannot approve or reject your own request.");
                    Require(scope.CanApprove(request.PayingCompanyId), 403, "Approval permission for the paying company is required.");
                    Require(request.Status == InternalPaymentRequestStatus.Submitted, 409, "Only a submitted request can be approved or rejected.");
                    Require(action != "reject" || !string.IsNullOrWhiteSpace(note), 400, "Enter a reason for rejection.");
                    return action == "approve" ? InternalPaymentRequestStatus.Approved : InternalPaymentRequestStatus.Rejected;
                case "cancel":
                    Require(scope.CanCancel(request.RequestingCompanyId) || scope.CanCancel(request.PayingCompanyId),
                        403, "Cancel permission for a participating company is required.");
                    Require(request.Status is InternalPaymentRequestStatus.Draft or InternalPaymentRequestStatus.Submitted,
                        409, "Only a draft or submitted request can be cancelled.");
                    return InternalPaymentRequestStatus.Cancelled;
                default:
                    throw new InternalPaymentRequestException(400, "Unknown request action.");
            }
        }

        private async Task<BankAccount> ValidateAsync(InternalPaymentRequestDto dto, CancellationToken cancellationToken)
        {
            Require(dto.RequestingCompanyId != dto.PayingCompanyId, 400, "Choose a different paying company.");
            Require(dto.RequestDate != default && dto.RequiredDate >= dto.RequestDate, 400, "Choose valid request and required dates.");
            Require(await database.Companies.AnyAsync(row => row.Id == dto.PayingCompanyId, cancellationToken), 400, "Choose an existing paying company.");
            var bank = await database.BankAccounts.AsNoTracking().SingleOrDefaultAsync(
                row => row.Id == dto.ReceivingBankAccountId && row.CompanyId == dto.RequestingCompanyId && row.IsActive,
                cancellationToken);
            Require(bank != null, 400, "Choose an active receiving bank account belonging to the requesting company.");
            Require(bank!.CurrencyDecimalPlaces is >= 0 and <= 4, 400, "The bank currency precision is invalid.");
            ValidateLines(dto.Lines, bank.CurrencyDecimalPlaces);
            return bank;
        }

        public static decimal ValidateLines(IReadOnlyList<InternalPaymentRequestLineDto>? lines, int decimalPlaces)
        {
            Require(decimalPlaces is >= 0 and <= 4, 400, "The currency precision is invalid.");
            Require(lines is { Count: > 0 and <= 100 }, 400, "Enter between 1 and 100 purpose lines.");
            foreach (var line in lines!)
            {
                Require(!string.IsNullOrWhiteSpace(line.Purpose) && line.Purpose.Trim().Length <= 200,
                    400, "Enter a purpose for every line.");
                Require(line.Amount > 0 && line.Amount < 100000000000000m &&
                    decimal.Round(line.Amount, decimalPlaces) == line.Amount,
                    400, "Enter a positive amount using the receiving bank currency's decimal places.");
            }
            var amount = lines.Sum(line => line.Amount);
            Require(amount < 100000000000000m, 400, "The total requested amount is too large.");
            return amount;
        }

        private static void Apply(InternalPaymentRequest request, InternalPaymentRequestDto dto, BankAccount bank)
        {
            request.RequestingCompanyId = dto.RequestingCompanyId;
            request.PayingCompanyId = dto.PayingCompanyId;
            request.ReceivingBankAccountId = bank.Id;
            request.Currency = bank.Currency;
            request.CurrencyDecimalPlaces = bank.CurrencyDecimalPlaces;
            request.RequestDate = dto.RequestDate;
            request.RequiredDate = dto.RequiredDate;
            request.Notes = (dto.Notes ?? "").Trim();
            request.Amount = ValidateLines(dto.Lines, bank.CurrencyDecimalPlaces);
            request.Lines.Clear();
            request.Lines.AddRange(dto.Lines.Select(line => new InternalPaymentRequestLine
            {
                Purpose = line.Purpose.Trim(),
                Amount = line.Amount
            }));
        }

        private static void Require(bool condition, int status, string message)
        {
            if (!condition)
            {
                throw new InternalPaymentRequestException(status, message);
            }
        }
    }
}
