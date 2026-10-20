using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PaymentMadeService.API.Data;
using PaymentMadeService.API.DTOs;
using PaymentMadeService.API.Models;
using PaymentMadeService.API.Repositories;

namespace PaymentMadeService.API.Services
{
    public class PaymentMadeService : IPaymentMadeService
    {
        private readonly PaymentMadeDbContext _context;
        private readonly IPaymentMadeRepository _repository;

        public PaymentMadeService(
            PaymentMadeDbContext context,
            IPaymentMadeRepository repository)
        {
            _context = context;
            _repository = repository;
        }

        public async Task<(List<PaymentMadeResponseDto> Data, int Total)> GetPagedAsync(
            PaymentMadeQueryDto query,
            CancellationToken cancellationToken)
        {
            Validator.ValidateObject(query, new ValidationContext(query), true);

            if (
                query.FromDate.HasValue &&
                query.ToDate.HasValue &&
                query.FromDate > query.ToDate)
            {
                throw new ArgumentException("From date cannot be after to date.");
            }

            if (query.VendorId == Guid.Empty)
            {
                throw new ArgumentException("Select a valid vendor.");
            }

            var (data, total) = await _repository.GetPagedAsync(
                query,
                cancellationToken);

            return (data.Select(ToResponse).ToList(), total);
        }

        public async Task<PaymentMadeResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var payment = await _repository.GetByIdAsync(id, cancellationToken);

            if (payment == null)
            {
                return null;
            }

            var response = ToResponse(payment);
            response.BillBalance = await GetBillBalanceAsync(
                payment.BillId,
                cancellationToken);

            return response;
        }

        public async Task<BillPaymentBalanceDto?> GetBillBalanceAsync(
            int billId,
            CancellationToken cancellationToken)
        {
            var bill = await _context.Bills
                .AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == billId, cancellationToken);

            if (bill == null)
            {
                return null;
            }

            var paid = await _context.PaymentsMade
                .Where(row =>
                    row.BillId == billId &&
                    row.Status == PaymentMadeStatus.Recorded)
                .SumAsync(row => (decimal?)row.Amount, cancellationToken) ?? 0m;

            var appliedCredit = await _context.CreditApplications
                .Where(row => row.BillId == billId && row.Status == "Applied")
                .SumAsync(row => (decimal?)row.Amount, cancellationToken) ?? 0m;

            return new BillPaymentBalanceDto
            {
                Id = bill.Id,
                VendorId = bill.VendorId,
                VendorName = bill.VendorName,
                BillNumber = bill.BillNumber,
                VendorInvoiceNumber = bill.VendorInvoiceNumber,
                Branch = bill.Branch,
                Currency = bill.Currency,
                CurrencyDecimalPlaces = bill.CurrencyDecimalPlaces,
                BillDate = bill.BillDate,
                Status = bill.Status,
                GrandTotal = bill.GrandTotal,
                PaidAmount = paid,
                AppliedCreditAmount = appliedCredit,
                RowVersion = bill.RowVersion
            };
        }

        public async Task<PaymentMadeResponseDto> CreateAsync(
            CreatePaymentMadeDto dto,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dto);
            Validator.ValidateObject(dto, new ValidationContext(dto), true);

            if (dto.RequestId == Guid.Empty || dto.VendorId == Guid.Empty)
            {
                throw new ArgumentException("A request ID and vendor are required.");
            }

            if (dto.PaymentDate == default)
            {
                throw new ArgumentException("Payment date is required.");
            }

            var fingerprint = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto))));

            var existing = await _repository.GetByRequestIdAsync(
                dto.RequestId,
                cancellationToken);

            if (existing != null)
            {
                return await CheckRequestAsync(existing, fingerprint, cancellationToken);
            }

            var bill = await GetBillBalanceAsync(dto.BillId, cancellationToken)
                ?? throw new ArgumentException("The selected bill was not found.");

            if (bill.VendorId != dto.VendorId)
            {
                throw new ArgumentException("The bill belongs to a different vendor.");
            }

            if (
                bill.CurrencyDecimalPlaces < 0 ||
                bill.CurrencyDecimalPlaces > 4 ||
                dto.Amount <= 0 ||
                dto.Amount > 99999999999999.9999m)
            {
                throw new ArgumentException("Enter a valid positive payment amount.");
            }

            if (decimal.Round(dto.Amount, bill.CurrencyDecimalPlaces) != dto.Amount)
            {
                throw new ArgumentException(
                    "Payment amount exceeds the bill currency's decimal precision.");
            }

            var mode = await _context.PaymentModes
                .SingleOrDefaultAsync(
                    row => row.Id == dto.PaymentModeId && row.IsActive,
                    cancellationToken)
                ?? throw new ArgumentException("Select an active payment mode.");

            var payment = new PaymentMade
            {
                RequestId = dto.RequestId,
                RequestFingerprint = fingerprint,
                BillId = bill.Id,
                BillNumber = bill.BillNumber ?? "",
                VendorInvoiceNumber = bill.VendorInvoiceNumber,
                VendorId = bill.VendorId,
                VendorName = bill.VendorName,
                Branch = bill.Branch,
                Currency = bill.Currency,
                CurrencyDecimalPlaces = bill.CurrencyDecimalPlaces,
                PaymentDate = dto.PaymentDate,
                Amount = dto.Amount,
                PaymentModeId = mode.Id,
                PaymentMode = mode.Name,
                Reference = dto.Reference?.Trim() ?? "",
                Notes = dto.Notes?.Trim() ?? ""
            };

            PaymentMade saved;

            try
            {
                saved = await _repository.RecordAsync(
                    payment,
                    dto.BillRowVersion,
                    cancellationToken);
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

                return await CheckRequestAsync(existing, fingerprint, cancellationToken);
            }

            return (await GetByIdAsync(saved.Id, cancellationToken))!;
        }

        private async Task<PaymentMadeResponseDto> CheckRequestAsync(
            PaymentMade payment,
            string fingerprint,
            CancellationToken cancellationToken)
        {
            if (payment.RequestFingerprint != fingerprint)
            {
                throw new InvalidOperationException(
                    "This request ID was already used for different payment details.");
            }

            return (await GetByIdAsync(payment.Id, cancellationToken))!;
        }

        public async Task<PaymentMadeResponseDto?> VoidAsync(
            int id,
            VoidPaymentMadeDto dto,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dto);
            Validator.ValidateObject(dto, new ValidationContext(dto), true);

            if (string.IsNullOrWhiteSpace(dto.Reason))
            {
                throw new ArgumentException("A reason is required when voiding a payment.");
            }

            var payment = await _repository.VoidAsync(id, dto, cancellationToken);

            if (payment == null)
            {
                return null;
            }

            return await GetByIdAsync(payment.Id, cancellationToken);
        }

        private static PaymentMadeResponseDto ToResponse(PaymentMade payment)
        {
            return new PaymentMadeResponseDto
            {
                Id = payment.Id,
                PaymentNumber = payment.PaymentNumber,
                BillId = payment.BillId,
                BillNumber = payment.BillNumber,
                VendorInvoiceNumber = payment.VendorInvoiceNumber,
                VendorId = payment.VendorId,
                VendorName = payment.VendorName,
                Branch = payment.Branch,
                PaymentDate = payment.PaymentDate,
                Amount = payment.Amount,
                Currency = payment.Currency,
                CurrencyDecimalPlaces = payment.CurrencyDecimalPlaces,
                PaymentModeId = payment.PaymentModeId,
                PaymentMode = payment.PaymentMode,
                Reference = payment.Reference,
                Notes = payment.Notes,
                Status = payment.Status,
                CreatedDate = payment.CreatedDate,
                VoidedDate = payment.VoidedDate,
                VoidReason = payment.VoidReason,
                RowVersion = payment.RowVersion
            };
        }
    }
}
