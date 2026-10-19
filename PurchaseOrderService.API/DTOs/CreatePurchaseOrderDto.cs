using System.ComponentModel.DataAnnotations;
using PurchaseOrderService.API.Models;

namespace PurchaseOrderService.API.DTOs;

public class CreatePurchaseOrderDto : PurchaseOrderFieldsDto, IValidatableObject
{
    public Guid RequestId { get; set; }
    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext context)
    {
        foreach (var error in base.Validate(context)) yield return error;
        if (RequestId == Guid.Empty) yield return new("Request ID is required.", [nameof(RequestId)]);
    }
}
