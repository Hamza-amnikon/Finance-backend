using System.ComponentModel.DataAnnotations;
using BillService.API.Models;

namespace BillService.API.DTOs
{
    public class UpdateBillDto : BillFieldsDto
    {
        [Required, MinLength(8), MaxLength(8)] public byte[] RowVersion { get; set; } = [];
    }
}
