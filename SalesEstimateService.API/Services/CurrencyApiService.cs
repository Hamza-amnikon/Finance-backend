using System.Net;
using System.Net.Http.Json;
using SalesEstimateService.API.DTOs;

namespace SalesEstimateService.API.Services
{
    public class CurrencyApiService : ICurrencyApiService
    {
        private readonly HttpClient _httpClient;

        public CurrencyApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<EstimateCurrencyDto?> GetByCodeAsync(
            string code,
            CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync(
                $"api/currencies/{Uri.EscapeDataString(code.Trim().ToUpperInvariant())}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            try
            {
                return await response.Content
                    .ReadFromJsonAsync<EstimateCurrencyDto>(
                        cancellationToken: cancellationToken)
                    ?? throw new HttpRequestException(
                        "SalesInvoiceService returned an empty currency response.");
            }
            catch (System.Text.Json.JsonException exception)
            {
                throw new HttpRequestException(
                    "SalesInvoiceService returned an invalid currency response.",
                    exception);
            }
        }
    }
}