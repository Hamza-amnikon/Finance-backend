using VendorCreditService.API.Models;

namespace VendorCreditService.API.Services
{
    public interface IVendorCreditCalculator
    {
        void Calculate(
            VendorCredit credit,
            int currencyDecimalPlaces);
    }
}