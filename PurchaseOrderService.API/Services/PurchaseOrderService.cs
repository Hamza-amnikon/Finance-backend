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

namespace PurchaseOrderService.API.Services;

public class PurchaseOrderService(PurchaseOrderDbContext db, IPurchaseOrderRepository repository,
    IPurchaseOrderCalculator calculator) : IPurchaseOrderService
{
    public async Task<(List<PurchaseOrderResponseDto> Data, int Total)> GetPagedAsync(PurchaseOrderQueryDto query, CancellationToken ct)
    {
        Validator.ValidateObject(query, new(query), true);
        var (data, total) = await repository.GetPagedAsync(query, ct);
        return (data.Select(ToResponse).ToList(), total);
    }
    public async Task<PurchaseOrderResponseDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var order = await repository.GetByIdAsync(id, ct);
        return order == null ? null : ToResponse(order);
    }
    public async Task<PurchaseOrderResponseDto> PreviewAsync(PurchaseOrderFieldsDto dto, CancellationToken ct) =>
        ToResponse(await PrepareAsync(dto, ct));

    public async Task<PurchaseOrderResponseDto> CreateAsync(CreatePurchaseOrderDto dto, CancellationToken ct)
    {
        Validate(dto);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto))));
        var existing = await repository.GetByRequestIdAsync(dto.RequestId, ct);
        if (existing != null) return CheckRequest(existing, fingerprint);
        var order = await PrepareAsync(dto, ct);
        order.RequestId = dto.RequestId;
        order.RequestFingerprint = fingerprint;
        try { return ToResponse(await repository.CreateAsync(order, ct)); }
        catch (DbUpdateException error) when (error.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
        {
            db.ChangeTracker.Clear();
            existing = await repository.GetByRequestIdAsync(dto.RequestId, ct);
            if (existing == null) throw;
            return CheckRequest(existing, fingerprint);
        }
    }
    private static PurchaseOrderResponseDto CheckRequest(PurchaseOrder order, string fingerprint)
    {
        if (order.RequestFingerprint != fingerprint)
            throw new InvalidOperationException("This request ID was already used for different order details.");
        return ToResponse(order);
    }
    public async Task<PurchaseOrderResponseDto?> UpdateAsync(int id, UpdatePurchaseOrderDto dto, CancellationToken ct)
    {
        Validate(dto);
        var order = await PrepareAsync(dto, ct);
        order.Id = id;
        var saved = await repository.UpdateDraftAsync(order, dto.RowVersion, ct);
        return saved == null ? null : ToResponse(saved);
    }
    public async Task<PurchaseOrderResponseDto?> ChangeStatusAsync(int id, PurchaseOrderStatus status, byte[] rowVersion, CancellationToken ct)
    {
        if (rowVersion == null || rowVersion.Length != 8) throw new ArgumentException("A valid row version is required.");
        var saved = await repository.ChangeStatusAsync(id, status, rowVersion, ValidateIssueAsync, ct);
        return saved == null ? null : ToResponse(saved);
    }
    private async Task ValidateIssueAsync(PurchaseOrder order, CancellationToken ct)
    {
        if (!await db.Vendors.AnyAsync(row => row.Id == order.VendorId && row.IsActive, ct))
            throw new InvalidOperationException("The vendor is inactive or unavailable.");
        if (!await db.Branches.AnyAsync(row => row.Name == order.Branch && row.IsActive, ct))
            throw new InvalidOperationException("The managing branch is inactive or unavailable.");
        var currency = await db.Currencies.SingleOrDefaultAsync(row => row.CurrencyCode == order.Currency && row.IsActive, ct);
        if (currency == null || currency.DecimalPlaces != order.CurrencyDecimalPlaces)
            throw new InvalidOperationException("Currency settings changed. Edit and save the draft before issuing.");
    }
    private async Task<PurchaseOrder> PrepareAsync(PurchaseOrderFieldsDto dto, CancellationToken ct)
    {
        Validate(dto);
        var vendor = await db.Vendors.SingleOrDefaultAsync(row => row.Id == dto.VendorId && row.IsActive, ct)
            ?? throw new ArgumentException("Select an active vendor.");
        var code = dto.Currency.Trim().ToUpperInvariant();
        var currency = await db.Currencies.SingleOrDefaultAsync(row => row.CurrencyCode == code && row.IsActive, ct)
            ?? throw new ArgumentException("Select an active currency.");
        if (!await db.Branches.AnyAsync(row => row.Name == dto.Branch.Trim() && row.IsActive, ct))
            throw new ArgumentException("Select an active managing branch.");
        if (currency.DecimalPlaces < 0 || currency.DecimalPlaces > 4) throw new ArgumentException("Invalid currency precision.");
        var address = string.Join(", ", new[] { vendor.Address, vendor.City, vendor.State, vendor.Country, vendor.PostalCode }
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));
        if (address.Length > 1000) throw new ArgumentException("Vendor address exceeds 1000 characters.");
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
            Notes = Clean(dto.Notes),
            AdditionalTerms = Clean(dto.AdditionalTerms),
            Branch = dto.Branch.Trim(),
            Currency = code,
            CurrencyDecimalPlaces = currency.DecimalPlaces,
            Items = dto.Items.Select(item => new PurchaseOrderItem
            {
                ItemName = item.ItemName.Trim(),
                Description = Clean(item.Description),
                Unit = item.Unit,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountEnabled = item.DiscountEnabled,
                DiscountAmount = item.DiscountAmount,
                TaxRate = item.TaxRate
            }).ToList()
        };
        calculator.Calculate(order, currency.DecimalPlaces);
        return order;
    }
    private static string Clean(string? value) => value?.Trim() ?? "";
    private static void Validate(PurchaseOrderFieldsDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        Validator.ValidateObject(dto, new(dto), true);
        foreach (var item in dto.Items)
        {
            if (item == null) throw new ArgumentException("An order line cannot be empty.");
            Validator.ValidateObject(item, new(item), true);
        }
    }
    private static PurchaseOrderResponseDto ToResponse(PurchaseOrder order) => new()
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
        Items = order.Items.OrderBy(item => item.LineNumber).Select(item => new PurchaseOrderItemResponseDto
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
        }).ToList()
    };
}
