using SalesEstimateService.API.Models;

namespace SalesEstimateService.API.Services
{
    public interface ISalesEstimateCalculator
    {
        void Calculate(
            SalesEstimate estimate,
            int currencyDecimalPlaces);
    }
}