using System.ComponentModel.DataAnnotations;

namespace BillService.API.DTOs
{
    public class ConvertPurchaseOrderDto
    {
        [Required, MinLength(8), MaxLength(8)] public byte[] RowVersion { get; set; } = [];
    }
}
