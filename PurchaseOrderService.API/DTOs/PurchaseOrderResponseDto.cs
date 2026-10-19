using System.ComponentModel.DataAnnotations;
using PurchaseOrderService.API.Models;

namespace PurchaseOrderService.API.DTOs;

public class PurchaseOrderResponseDto
{
    public int Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid VendorId { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public string VendorName { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string BillingAddress { get; set; } = "";
    public string ShippingAddress { get; set; } = "";
    public DateOnly OrderDate { get; set; }
    public DateOnly? ExpectedDeliveryDate { get; set; }
    public string Reference { get; set; } = "";
    public string PaymentTerms { get; set; } = "";
    public string Notes { get; set; } = "";
    public string AdditionalTerms { get; set; } = "";
    public string Branch { get; set; } = "";
    public string Currency { get; set; } = "";
    public int CurrencyDecimalPlaces { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public PurchaseOrderStatus Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? CancelledDate { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<PurchaseOrderItemResponseDto> Items { get; set; } = [];
}
