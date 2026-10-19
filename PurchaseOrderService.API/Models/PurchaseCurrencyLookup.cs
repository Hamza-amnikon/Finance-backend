namespace PurchaseOrderService.API.Models;

public class PurchaseCurrencyLookup
{
    public string CurrencyCode { get; set; } = "";
    public string CurrencyName { get; set; } = "";
    public int DecimalPlaces { get; set; }
    public bool IsActive { get; set; }
}
