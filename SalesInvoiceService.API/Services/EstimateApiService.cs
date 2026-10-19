using System.Net;
using SalesInvoiceService.API.DTOs;

namespace SalesInvoiceService.API.Services
{
    public class EstimateApiService : IEstimateApiService
    {
        private readonly HttpClient _httpClient;

        public EstimateApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<EstimateForInvoiceDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync($"api/sales-estimates/{id}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            try
            {
                return await response.Content.ReadFromJsonAsync<EstimateForInvoiceDto>(cancellationToken: cancellationToken)
                    ?? throw new HttpRequestException("Estimate service returned an empty response.");
            }
            catch (System.Text.Json.JsonException exception)
            {
                throw new HttpRequestException("Estimate service returned invalid data.", exception);
            }
        }
    }
}
