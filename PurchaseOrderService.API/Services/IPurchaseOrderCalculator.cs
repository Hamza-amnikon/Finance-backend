using PurchaseOrderService.API.Models;
namespace PurchaseOrderService.API.Services;

public interface IPurchaseOrderCalculator
{
    void Calculate(PurchaseOrder order, int currencyDecimalPlaces);
}
