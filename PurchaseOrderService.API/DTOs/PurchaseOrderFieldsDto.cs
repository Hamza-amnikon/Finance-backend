using System.ComponentModel.DataAnnotations;
using PurchaseOrderService.API.Models;

namespace PurchaseOrderService.API.DTOs;

public class PurchaseOrderFieldsDto : IValidatableObject
{
    public Guid VendorId { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateOnly? ExpectedDeliveryDate { get; set; }
    [Required, MaxLength(20)] public string Branch { get; set; } = "";
    [Required, RegularExpression("^[A-Za-z]{3}$")] public string Currency { get; set; } = "";
    [MaxLength(1000)] public string? BillingAddress { get; set; }
    [MaxLength(1000)] public string ShippingAddress { get; set; } = "";
    [MaxLength(200)] public string Reference { get; set; } = "";
    [MaxLength(200)] public string PaymentTerms { get; set; } = "";
    [MaxLength(2000)] public string Notes { get; set; } = "";
    [MaxLength(2000)] public string AdditionalTerms { get; set; } = "";
    [Required, MinLength(1), MaxLength(100)]
    public List<PurchaseOrderItemRequestDto> Items { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (VendorId == Guid.Empty) yield return new("Select a vendor.", [nameof(VendorId)]);
        if (OrderDate == default) yield return new("Order date is required.", [nameof(OrderDate)]);
        if (ExpectedDeliveryDate.HasValue && ExpectedDeliveryDate.Value < OrderDate)
            yield return new("Delivery date cannot precede the order date.", [nameof(ExpectedDeliveryDate)]);
    }
}
