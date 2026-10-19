using System.ComponentModel.DataAnnotations;

namespace PurchaseOrderService.API.Models;

public class PurchaseOrderNumberCounter
{
    [MaxLength(6)] public string PeriodKey { get; set; } = "";
    public int LastNumber { get; set; }
}
