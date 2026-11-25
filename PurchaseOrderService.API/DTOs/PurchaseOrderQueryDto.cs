using SparkFinance.Http;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PurchaseOrderService.API.Models;

namespace PurchaseOrderService.API.DTOs;

public class PurchaseOrderQueryDto : IFinanceBranchQuery
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string[]? AllowedBranches { get; set; }

    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 25;
    [MaxLength(200)]
    public string? Search { get; set; }
    [EnumDataType(typeof(PurchaseOrderStatus))]
    public PurchaseOrderStatus? Status { get; set; }

}
