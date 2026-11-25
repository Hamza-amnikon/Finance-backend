using System.ComponentModel.DataAnnotations;
using BillService.API.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BillService.API.DTOs
{
    public class BillQueryDto
    {
        [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
        [Range(1, 100)] public int PageSize { get; set; } = 25;
        [MaxLength(200)]
        public string? Search { get; set; }
        [EnumDataType(typeof(BillStatus))]
        public BillStatus? Status { get; set; }
        [BindNever]
        public string[]? AllowedBranches { get; set; }
    }
}
