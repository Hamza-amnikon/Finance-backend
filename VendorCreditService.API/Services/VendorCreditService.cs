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
    public class VendorCreditService : IVendorCreditService
    {
        private readonly VendorCreditDbContext _context;
        private readonly IVendorCreditRepository _repository;
        private readonly IVendorCreditCalculator _calculator;

        public VendorCreditService(
            VendorCreditDbContext context,
            IVendorCreditRepository repository,
            IVendorCreditCalculator calculator)
        {
            _context = context;
            _repository = repository;
            _calculator = calculator;
        }

        public async Task<(
            List<VendorCreditResponseDto> Data,
            int Total)> GetPagedAsync(
                VendorCreditQueryDto query,
                CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            Validator.ValidateObject(
                query,
                new ValidationContext(query),
                true);

            var (data, total) = await _repository.GetPagedAsync(
                query,
                cancellationToken);

            var ids = data.Select(row => row.Id).ToList();

            var amounts = await _context.Applications
                .Where(row =>
                    ids.Contains(row.VendorCreditId) &&
                    row.Status == CreditApplicationStatus.Applied)
                .GroupBy(row => row.VendorCreditId)
                .Select(group => new
                {
                    CreditId = group.Key,
                    Amount = group.Sum(row => row.Amount)
                })
                .ToDictionaryAsync(row => row.CreditId, row => row.Amount, cancellationToken);

            var responses = data.Select(row =>
            {
                var response = ToResponse(row);
                response.AppliedAmount = amounts.GetValueOrDefault(row.Id);
                return response;
            }).ToList();

            return (responses, total);
        }

        public async Task<VendorCreditResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var credit = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (credit == null)
            {
                return null;
            }

            return await EnrichAsync(ToResponse(credit), cancellationToken);
        }

        public async Task<VendorCreditResponseDto> PreviewAsync(
            VendorCreditFieldsDto dto,
            CancellationToken cancellationToken)
        {
            var credit = await PrepareAsync(
                dto,
                cancellationToken);

            return await EnrichAsync(ToResponse(credit), cancellationToken);
        }

        public async Task<VendorCreditResponseDto> CreateAsync(
            CreateVendorCreditDto dto,
            CancellationToken cancellationToken)
        {
            Validate(dto);

            var fingerprint = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        JsonSerializer.Serialize(dto))));

            var existing = await _repository.GetByRequestIdAsync(
                dto.RequestId,
                cancellationToken);

            if (existing != null)
            {
                return await EnrichAsync(
                    CheckRequest(existing, fingerprint),
                    cancellationToken);
            }

            var credit = await PrepareAsync(
                dto,
                cancellationToken);

            credit.RequestId = dto.RequestId;
            credit.RequestFingerprint = fingerprint;

            try
            {
                var saved = await _repository.CreateAsync(
                    credit,
                    cancellationToken);

                return await EnrichAsync(ToResponse(saved), cancellationToken);
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

                return await EnrichAsync(
                    CheckRequest(existing, fingerprint),
                    cancellationToken);
            }
        }

        public async Task<VendorCreditResponseDto?> UpdateAsync(
            int id,
            UpdateVendorCreditDto dto,
            CancellationToken cancellationToken)
        {
            Validate(dto);
            ValidateRowVersion(dto.RowVersion);

            var credit = await PrepareAsync(
                dto,
                cancellationToken);

            credit.Id = id;

            var saved = await _repository.UpdateDraftAsync(
                credit,
                dto.RowVersion,
                cancellationToken);

            if (saved == null)
            {
                return null;
            }

            return await EnrichAsync(ToResponse(saved), cancellationToken);
        }

        public async Task<VendorCreditResponseDto?> ChangeStatusAsync(
            int id,
            VendorCreditStatus status,
            byte[] rowVersion,
            CancellationToken cancellationToken)
        {
            ValidateRowVersion(rowVersion);

            if (
                status != VendorCreditStatus.Issued &&
                status != VendorCreditStatus.Cancelled)
            {
                throw new ArgumentException(
                    "Unsupported vendor credit action.");
            }

            var saved = await _repository.ChangeStatusAsync(
                id,
                status,
                rowVersion,
                ValidateIssueAsync,
                cancellationToken);

            if (saved == null)
            {
                return null;
            }

            return await EnrichAsync(ToResponse(saved), cancellationToken);
        }

        private async Task<VendorCreditResponseDto> EnrichAsync(
            VendorCreditResponseDto response,
            CancellationToken cancellationToken)
        {
            if (response.Id == 0)
            {
                return response;
            }

            var applications = await _context.Applications
                .AsNoTracking()
                .Where(row => row.VendorCreditId == response.Id)
                .OrderByDescending(row => row.Id)
                .ToListAsync(cancellationToken);

            response.Applications = applications
                .Select(CreditApplicationService.ToResponse)
                .ToList();

            response.AppliedAmount = applications
                .Where(row => row.Status == CreditApplicationStatus.Applied)
                .Sum(row => row.Amount);

            return response;
        }

        private static VendorCreditResponseDto CheckRequest(
            VendorCredit credit,
            string fingerprint)
        {
            if (credit.RequestFingerprint != fingerprint)
            {
                throw new InvalidOperationException(
                    "This request ID was already used for different vendor credit details.");
            }

            return ToResponse(credit);
        }

        private async Task<VendorCredit> PrepareAsync(
            VendorCreditFieldsDto dto,
            CancellationToken cancellationToken)
        {
            Validate(dto);

            var vendor = await _context.Vendors
                .SingleOrDefaultAsync(
                    row =>
                        row.Id == dto.VendorId &&
                        row.IsActive,
                    cancellationToken);

            if (vendor == null)
            {
                throw new ArgumentException(
                    "Select an active vendor.");
            }

            var branch = dto.Branch.Trim();
            var currencyCode = dto.Currency.Trim().ToUpperInvariant();

            var branchActive = await _context.Branches
                .AnyAsync(
                    row =>
                        row.Name == branch &&
                        row.IsActive,
                    cancellationToken);

            if (!branchActive)
            {
                throw new ArgumentException(
                    "Select an active managing branch.");
            }

            var currency = await _context.Currencies
                .SingleOrDefaultAsync(
                    row =>
                        row.CurrencyCode == currencyCode &&
                        row.IsActive,
                    cancellationToken);

            if (currency == null)
            {
                throw new ArgumentException(
                    "Select an active currency.");
            }

            if (
                currency.DecimalPlaces < 0 ||
                currency.DecimalPlaces > 4)
            {
                throw new ArgumentException(
                    "Invalid currency precision.");
            }

            await ValidateBillAsync(
                dto.BillId,
                vendor.Id,
                branch,
                currencyCode,
                currency.DecimalPlaces,
                cancellationToken);

            var address = string.Join(
                ", ",
                new[]
                {
                    vendor.Address,
                    vendor.City,
                    vendor.State,
                    vendor.Country,
                    vendor.PostalCode
                }
                    .Where(value =>
                        !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim()));

            var billingAddress =
                dto.BillingAddress?.Trim() ?? address;

            if (billingAddress.Length > 1000)
            {
                throw new ArgumentException(
                    "Vendor billing address exceeds 1000 characters.");
            }

            var credit = new VendorCredit
            {
                VendorId = vendor.Id,
                BillId = dto.BillId,
                VendorCreditNoteNumber = Clean(
                    dto.VendorCreditNoteNumber),
                VendorName = vendor.Name,
                CompanyName = vendor.CompanyName,
                BillingAddress = billingAddress,
                CreditDate = dto.CreditDate,
                Reference = Clean(dto.Reference),
                Reason = Clean(dto.Reason),
                Notes = Clean(dto.Notes),
                Branch = branch,
                Currency = currencyCode,
                CurrencyDecimalPlaces = currency.DecimalPlaces,

                Items = dto.Items
                    .Select(item => new VendorCreditItem
                    {
                        ItemName = item.ItemName.Trim(),
                        Description = Clean(item.Description),
                        Unit = item.Unit,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        DiscountEnabled = item.DiscountEnabled,
                        DiscountAmount = item.DiscountAmount,
                        TaxRate = item.TaxRate
                    })
                    .ToList()
            };

            _calculator.Calculate(
                credit,
                currency.DecimalPlaces);

            return credit;
        }

        private async Task ValidateIssueAsync(
            VendorCredit credit,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(
                credit.VendorCreditNoteNumber))
            {
                throw new InvalidOperationException(
                    "Enter the vendor credit note number before issuing the credit.");
            }

            if (credit.GrandTotal <= 0)
            {
                throw new InvalidOperationException(
                    "Vendor credit total must be greater than zero before issuing.");
            }

            var vendorActive = await _context.Vendors
                .AnyAsync(
                    row =>
                        row.Id == credit.VendorId &&
                        row.IsActive,
                    cancellationToken);

            if (!vendorActive)
            {
                throw new InvalidOperationException(
                    "The vendor is inactive or unavailable.");
            }

            var branchActive = await _context.Branches
                .AnyAsync(
                    row =>
                        row.Name == credit.Branch &&
                        row.IsActive,
                    cancellationToken);

            if (!branchActive)
            {
                throw new InvalidOperationException(
                    "The managing branch is inactive or unavailable.");
            }

            var currency = await _context.Currencies
                .SingleOrDefaultAsync(
                    row =>
                        row.CurrencyCode == credit.Currency &&
                        row.IsActive,
                    cancellationToken);

            if (
                currency == null ||
                currency.DecimalPlaces != credit.CurrencyDecimalPlaces)
            {
                throw new InvalidOperationException(
                    "Currency settings changed. Edit and save the draft before issuing.");
            }

            await ValidateBillAsync(
                credit.BillId,
                credit.VendorId,
                credit.Branch,
                credit.Currency,
                credit.CurrencyDecimalPlaces,
                cancellationToken);
        }

        private async Task ValidateBillAsync(
            int? billId,
            Guid vendorId,
            string branch,
            string currency,
            int currencyDecimalPlaces,
            CancellationToken cancellationToken)
        {
            if (!billId.HasValue)
            {
                return;
            }

            var bill = await _context.Bills
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    row => row.Id == billId.Value,
                    cancellationToken);

            if (bill == null)
            {
                throw new ArgumentException(
                    "The selected bill was not found.");
            }

            if (bill.VendorId != vendorId)
            {
                throw new ArgumentException(
                    "The selected bill belongs to a different vendor.");
            }

            if (bill.Status != "Issued")
            {
                throw new ArgumentException(
                    "Select an issued bill.");
            }

            if (!string.Equals(
                bill.Branch,
                branch,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The selected bill belongs to a different managing branch.");
            }

            if (!string.Equals(
                bill.Currency,
                currency,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Vendor credit currency must match the selected bill.");
            }

            if (bill.CurrencyDecimalPlaces != currencyDecimalPlaces)
            {
                throw new ArgumentException(
                    "Currency precision must match the selected bill.");
            }
        }

        private static void Validate(
            VendorCreditFieldsDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            Validator.ValidateObject(
                dto,
                new ValidationContext(dto),
                true);

            foreach (var item in dto.Items)
            {
                if (item == null)
                {
                    throw new ArgumentException(
                        "A credit line cannot be empty.");
                }

                Validator.ValidateObject(
                    item,
                    new ValidationContext(item),
                    true);
            }
        }

        private static void ValidateRowVersion(
            byte[] rowVersion)
        {
            if (rowVersion == null || rowVersion.Length != 8)
            {
                throw new ArgumentException(
                    "A valid row version is required.");
            }
        }

        private static string Clean(string? value)
        {
            return value?.Trim() ?? "";
        }

        private static VendorCreditResponseDto ToResponse(
            VendorCredit credit)
        {
            return new VendorCreditResponseDto
            {
                Id = credit.Id,
                RequestId = credit.RequestId,
                VendorId = credit.VendorId,
                BillId = credit.BillId,
                VendorCreditNumber = credit.VendorCreditNumber,
                VendorCreditNoteNumber = credit.VendorCreditNoteNumber,
                VendorName = credit.VendorName,
                CompanyName = credit.CompanyName,
                BillingAddress = credit.BillingAddress,
                CreditDate = credit.CreditDate,
                Reference = credit.Reference,
                Reason = credit.Reason,
                Notes = credit.Notes,
                Branch = credit.Branch,
                Currency = credit.Currency,
                CurrencyDecimalPlaces = credit.CurrencyDecimalPlaces,
                SubTotal = credit.SubTotal,
                DiscountTotal = credit.DiscountTotal,
                TaxTotal = credit.TaxTotal,
                GrandTotal = credit.GrandTotal,
                Status = credit.Status,
                CreatedDate = credit.CreatedDate,
                ModifiedDate = credit.ModifiedDate,
                IssuedDate = credit.IssuedDate,
                CancelledDate = credit.CancelledDate,
                RowVersion = credit.RowVersion,

                Items = credit.Items
                    .OrderBy(item => item.LineNumber)
                    .Select(item => new VendorCreditItemResponseDto
                    {
                        Id = item.Id,
                        LineNumber = item.LineNumber,
                        ItemName = item.ItemName,
                        Description = item.Description,
                        Unit = item.Unit,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        DiscountEnabled = item.DiscountEnabled,
                        DiscountAmount = item.DiscountAmount,
                        TaxRate = item.TaxRate,
                        LineSubTotal = item.LineSubTotal,
                        TaxAmount = item.TaxAmount,
                        LineTotal = item.LineTotal
                    })
                    .ToList()
            };
        }
    }
}
