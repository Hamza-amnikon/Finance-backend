using BillService.API.Models;
namespace BillService.API.Services
{
    public interface IBillCalculator
    {
        void Calculate(Bill bill, int currencyDecimalPlaces);
    }
}
