using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PurchaseOrderService.API.Data;
using PurchaseOrderService.API.DTOs;
using PurchaseOrderService.API.Models;
using PurchaseOrderService.API.Repositories;

namespace PurchaseOrderService.API.Services
{
    public class PurchaseOrderService : IPurchaseOrderService
    {
        private readonly PurchaseOrderDbContext _context;
        private readonly IPurchaseOrderRepository _repository;
        private readonly IPurchaseOrderCalculator _calculator;

        public PurchaseOrderService(
            PurchaseOrderDbContext context,
            IPurchaseOrderRepository repository,
            IPurchaseOrderCalculator calculator)
        {
            _context = context;
            _repository = repository;
            _calculator = calculator;
        }

        public async Task<(
            List<PurchaseOrderResponseDto> Data,
            int Total)> GetPagedAsync(
                PurchaseOrderQueryDto query,
                CancellationToken ct)
        {
            Validator.ValidateObject(
                query,
                new ValidationContext(query),
                true);

            var (data, total) = await _repository.GetPagedAsync(
                query,
                ct);

            return (
                data.Select(ToResponse).ToList(),
                total);
        }

        public async Task<PurchaseOrderResponseDto?> GetByIdAsync(
            int id,
            CancellationToken ct)
        {
            var order = await _repository.GetByIdAsync(
                id,
                ct);

            if (order == null)
            {
                return null;
            }

            return ToResponse(order);
        }

        public async Task<PurchaseOrderResponseDto> PreviewAsync(
            PurchaseOrderFieldsDto dto,
            CancellationToken ct)
        {
            var order = await PrepareAsync(dto, ct);

            return ToResponse(order);
        }

        public async Task<PurchaseOrderResponseDto> CreateAsync(
            CreatePurchaseOrderDto dto,
            CancellationToken ct)
        {
            Validate(dto);

            var fingerprint = Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        JsonSerializer.Serialize(dto))));

            var existing = await _repository.GetByRequestIdAsync(
                dto.RequestId,
                ct);

            if (existing != null)
            {
                return CheckRequest(existing, fingerprint);
            }

            var order = await PrepareAsync(dto, ct);

            order.RequestId = dto.RequestId;
            order.RequestFingerprint = fingerprint;

            try
            {
                var saved = await _repository.CreateAsync(
                    order,
                    ct);

                return ToResponse(saved);
            }
            catch (DbUpdateException error) when (
                error.InnerException is SqlException sql &&
                (sql.Number == 2601 || sql.Number == 2627))
            {
                _context.ChangeTracker.Clear();

                existing = await _repository.GetByRequestIdAsync(
                    dto.RequestId,
                    ct);

                if (existing == null)
                {
                    throw;
                }

                return CheckRequest(existing, fingerprint);
            }
        }

        public async Task<PurchaseOrderResponseDto?> UpdateAsync(
            int id,
            UpdatePurchaseOrderDto dto,
            CancellationToken ct)
        {
            Validate(dto);

            var order = await PrepareAsync(dto, ct);

            order.Id = id;

            var saved = await _repository.UpdateDraftAsync(
                order,
                dto.RowVersion,
                ct);

            if (saved == null)
            {
                return null;
            }

            return ToResponse(saved);
        }

        public async Task<PurchaseOrderResponseDto?> ChangeStatusAsync(
            int id,
            PurchaseOrderStatus status,
            byte[] rowVersion,
            CancellationToken ct)
        {
            if (rowVersion == null || rowVersion.Length != 8)
            {
                throw new ArgumentException(
                    "A valid row version is required.");
            }

            var saved = await _repository.ChangeStatusAsync(
                id,
                status,
                rowVersion,
                ValidateIssueAsync,
                ct);

            if (saved == null)
            {
                return null;
            }

            return ToResponse(saved);
        }

        private static PurchaseOrderResponseDto CheckRequest(
            PurchaseOrder order,
            string fingerprint)
        {
            if (order.RequestFingerprint != fingerprint)
            {
                throw new InvalidOperationException(
                    "This request ID was already used for different order details.");
            }

            return ToResponse(order);
        }

        private async Task ValidateIssueAsync(
            PurchaseOrder order,
            CancellationToken ct)
        {
            var vendorActive = await _context.Vendors.AnyAsync(
                row =>
                    row.Id == order.VendorId &&
                    row.IsActive,
                ct);

            if (!vendorActive)
            {
                throw new InvalidOperationException(
                    "The vendor is inactive or unavailable.");
            }

            var branchActive = await _context.Branches.AnyAsync(
                row =>
                    row.Name == order.Branch &&
                    row.IsActive,
                ct);

            if (!branchActive)
            {
                throw new InvalidOperationException(
                    "The managing branch is inactive or unavailable.");
            }

            var currency = await _context.Currencies
                .SingleOrDefaultAsync(
                    row =>
                        row.CurrencyCode == order.Currency &&
                        row.IsActive,
                    ct);

            if (
                currency == null ||
                currency.DecimalPlaces != order.CurrencyDecimalPlaces)
            {
                throw new InvalidOperationException(
                    "Currency settings changed. Edit and save the draft before issuing.");
            }

            if (order.PaymentModeId.HasValue)
            {
                var paymentModeActive = await _context.PaymentModes
                    .AnyAsync(
                        row =>
                            row.Id == order.PaymentModeId.Value &&
                            row.IsActive,
                        ct);

                if (!paymentModeActive)
                {
                    throw new InvalidOperationException(
                        "The payment mode is inactive or unavailable. Edit and save the draft before issuing.");
                }
            }
        }

        private async Task<PurchaseOrder> PrepareAsync(
            PurchaseOrderFieldsDto dto,
            CancellationToken ct)
        {
            Validate(dto);

            var vendor = await _context.Vendors
                .SingleOrDefaultAsync(
                    row =>
                        row.Id == dto.VendorId &&
                        row.IsActive,
                    ct);

            if (vendor == null)
            {
                throw new ArgumentException(
                    "Select an active vendor.");
            }

            var code = dto.Currency.Trim().ToUpperInvariant();

            var currency = await _context.Currencies
                .SingleOrDefaultAsync(
                    row =>
                        row.CurrencyCode == code &&
                        row.IsActive,
                    ct);

            if (currency == null)
            {
                throw new ArgumentException(
                    "Select an active currency.");
            }

            var branchActive = await _context.Branches.AnyAsync(
                row =>
                    row.Name == dto.Branch.Trim() &&
                    row.IsActive,
                ct);

            if (!branchActive)
            {
                throw new ArgumentException(
                    "Select an active managing branch.");
            }

            if (
                currency.DecimalPlaces < 0 ||
                currency.DecimalPlaces > 4)
            {
                throw new ArgumentException(
                    "Invalid currency precision.");
            }

            if (dto.PaymentModeId.HasValue)
            {
                var paymentModeActive = await _context.PaymentModes
                    .AnyAsync(
                        row =>
                            row.Id == dto.PaymentModeId.Value &&
                            row.IsActive,
                        ct);

                if (!paymentModeActive)
                {
                    throw new ArgumentException(
                        "Select an active payment mode.");
                }
            }

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

            if (address.Length > 1000)
            {
                throw new ArgumentException(
                    "Vendor address exceeds 1000 characters.");
            }

            var order = new PurchaseOrder
            {
                VendorId = vendor.Id,
                VendorName = vendor.Name,
                CompanyName = vendor.CompanyName,
                BillingAddress = dto.BillingAddress?.Trim() ?? address,
                ShippingAddress = Clean(dto.ShippingAddress),
                OrderDate = dto.OrderDate,
                ExpectedDeliveryDate = dto.ExpectedDeliveryDate,
                Reference = Clean(dto.Reference),
                PaymentTerms = Clean(dto.PaymentTerms),
                PaymentModeId = dto.PaymentModeId,
                Notes = Clean(dto.Notes),
                AdditionalTerms = Clean(dto.AdditionalTerms),
                Branch = dto.Branch.Trim(),
                Currency = code,
                CurrencyDecimalPlaces = currency.DecimalPlaces,

                Items = dto.Items
                    .Select(item => new PurchaseOrderItem
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
                order,
                currency.DecimalPlaces);

            return order;
        }

        private static string Clean(string? value)
        {
            return value?.Trim() ?? "";
        }

        private static void Validate(PurchaseOrderFieldsDto dto)
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
                        "An order line cannot be empty.");
                }

                Validator.ValidateObject(
                    item,
                    new ValidationContext(item),
                    true);
            }
        }

        private static PurchaseOrderResponseDto ToResponse(
            PurchaseOrder order)
        {
            return new PurchaseOrderResponseDto
            {
                Id = order.Id,
                RequestId = order.RequestId,
                VendorId = order.VendorId,
                PurchaseOrderNumber = order.PurchaseOrderNumber,
                VendorName = order.VendorName,
                CompanyName = order.CompanyName,
                BillingAddress = order.BillingAddress,
                ShippingAddress = order.ShippingAddress,
                OrderDate = order.OrderDate,
                ExpectedDeliveryDate = order.ExpectedDeliveryDate,
                Reference = order.Reference,
                PaymentTerms = order.PaymentTerms,
                PaymentModeId = order.PaymentModeId,
                Notes = order.Notes,
                AdditionalTerms = order.AdditionalTerms,
                Branch = order.Branch,
                Currency = order.Currency,
                CurrencyDecimalPlaces = order.CurrencyDecimalPlaces,
                SubTotal = order.SubTotal,
                DiscountTotal = order.DiscountTotal,
                TaxTotal = order.TaxTotal,
                GrandTotal = order.GrandTotal,
                Status = order.Status,
                CreatedDate = order.CreatedDate,
                ModifiedDate = order.ModifiedDate,
                IssuedDate = order.IssuedDate,
                CancelledDate = order.CancelledDate,
                RowVersion = order.RowVersion,

                Items = order.Items
                    .OrderBy(item => item.LineNumber)
                    .Select(item => new PurchaseOrderItemResponseDto
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