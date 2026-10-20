using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VendorCreditService.API.Data;
using VendorCreditService.API.DTOs;
using VendorCreditService.API.Models;
using VendorCreditService.API.Repositories;

namespace VendorCreditService.API.Services
{
    public class CreditApplicationService : ICreditApplicationService
    {
        private readonly VendorCreditDbContext _context;
        private readonly ICreditApplicationRepository _repository;

        public CreditApplicationService(
            VendorCreditDbContext context,
            ICreditApplicationRepository repository)
        {
            _context = context;
            _repository = repository;
        }

        public async Task<(List<CreditBillBalanceDto> Data, int Total)> GetBillsAsync(
            int creditId,
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                throw new ArgumentException("Invalid page or page size.");
            }

            var credit = await _context.VendorCredits
                .AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == creditId, cancellationToken)
                ?? throw new ArgumentException("Vendor credit not found.");

            if (credit.Status != VendorCreditStatus.Issued)
            {
                throw new InvalidOperationException("Issue the credit before applying it.");
            }

            var query = _context.Bills
                .AsNoTracking()
                .Where(row =>
                    row.Status == "Issued" &&
                    row.VendorId == credit.VendorId &&
                    row.Branch == credit.Branch &&
                    row.Currency == credit.Currency &&
                    row.CurrencyDecimalPlaces == credit.CurrencyDecimalPlaces);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var text = search.Trim();

                query = query.Where(row =>
                    (row.BillNumber != null && row.BillNumber.Contains(text)) ||
                    row.VendorInvoiceNumber.Contains(text));
            }

            var balances = query.Select(bill => new CreditBillBalanceDto
            {
                Id = bill.Id,
                BillNumber = bill.BillNumber,
                VendorInvoiceNumber = bill.VendorInvoiceNumber,
                BillDate = bill.BillDate,
                GrandTotal = bill.GrandTotal,
                Currency = bill.Currency,
                CurrencyDecimalPlaces = bill.CurrencyDecimalPlaces,
                RowVersion = bill.RowVersion,
                PaidAmount = _context.Payments
                    .Where(row => row.BillId == bill.Id && row.Status == "Recorded")
                    .Sum(row => (decimal?)row.Amount) ?? 0m,
                AppliedCreditAmount = _context.Applications
                    .Where(row =>
                        row.BillId == bill.Id &&
                        row.Status == CreditApplicationStatus.Applied)
                    .Sum(row => (decimal?)row.Amount) ?? 0m
            })
                .Where(row => row.GrandTotal > row.PaidAmount + row.AppliedCreditAmount);

            var total = await balances.CountAsync(cancellationToken);
            var offset = ((long)page - 1) * pageSize;

            var data = offset >= total
                ? new List<CreditBillBalanceDto>()
                : await balances
                    .OrderBy(row => row.BillDate)
                    .ThenBy(row => row.Id)
                    .Skip((int)offset)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

            return (data, total);
        }

        public async Task<CreditApplicationResponseDto> ApplyAsync(
            int creditId,
            ApplyVendorCreditDto dto,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dto);
            Validator.ValidateObject(dto, new ValidationContext(dto), true);

            if (
                dto.RequestId == Guid.Empty ||
                dto.ApplicationDate == default ||
                dto.Amount <= 0 ||
                dto.Amount > 99999999999999.9999m)
            {
                throw new ArgumentException(
                    "Provide a request ID, application date and positive amount.");
            }

            var fingerprint = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        JsonSerializer.Serialize(new { creditId, dto }))));

            var existing = await _repository.GetByRequestIdAsync(
                dto.RequestId,
                cancellationToken);

            if (existing != null)
            {
                return CheckRequest(existing, fingerprint);
            }

            var application = new VendorCreditApplication
            {
                RequestId = dto.RequestId,
                RequestFingerprint = fingerprint,
                VendorCreditId = creditId,
                BillId = dto.BillId,
                Amount = dto.Amount,
                ApplicationDate = dto.ApplicationDate,
                Notes = dto.Notes?.Trim() ?? ""
            };

            try
            {
                var saved = await _repository.ApplyAsync(
                    application,
                    dto.CreditRowVersion,
                    dto.BillRowVersion,
                    cancellationToken);

                return ToResponse(saved);
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is SqlException sql &&
                (sql.Number == 2601 || sql.Number == 2627))
            {
                _context.ChangeTracker.Clear();

                existing = await _repository.GetByRequestIdAsync(
                    dto.RequestId,
                    cancellationToken);

                if (existing == null)
                {
                    throw;
                }

                return CheckRequest(existing, fingerprint);
            }
        }

        public async Task<CreditApplicationResponseDto?> VoidAsync(
            int creditId,
            int applicationId,
            VoidCreditApplicationDto dto,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dto);
            Validator.ValidateObject(dto, new ValidationContext(dto), true);

            var application = await _repository.VoidAsync(
                creditId,
                applicationId,
                dto,
                cancellationToken);

            return application == null ? null : ToResponse(application);
        }

        private static CreditApplicationResponseDto CheckRequest(
            VendorCreditApplication application,
            string fingerprint)
        {
            if (application.RequestFingerprint != fingerprint)
            {
                throw new InvalidOperationException(
                    "This request ID was already used for different application details.");
            }

            return ToResponse(application);
        }

        public static CreditApplicationResponseDto ToResponse(
            VendorCreditApplication application)
        {
            return new CreditApplicationResponseDto
            {
                Id = application.Id,
                VendorCreditId = application.VendorCreditId,
                VendorCreditNumber = application.VendorCreditNumber,
                BillId = application.BillId,
                BillNumber = application.BillNumber,
                Amount = application.Amount,
                Currency = application.Currency,
                ApplicationDate = application.ApplicationDate,
                Notes = application.Notes,
                Status = application.Status,
                CreatedDate = application.CreatedDate,
                VoidedDate = application.VoidedDate,
                VoidReason = application.VoidReason,
                RowVersion = application.RowVersion
            };
        }
    }
}
