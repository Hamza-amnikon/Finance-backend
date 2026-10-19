using System.ComponentModel.DataAnnotations;
using PurchaseOrderService.API.Models;

namespace PurchaseOrderService.API.DTOs;

public class PurchaseOrderActionDto
{
    [Required, MinLength(8), MaxLength(8)] public byte[] RowVersion { get; set; } = [];
}
