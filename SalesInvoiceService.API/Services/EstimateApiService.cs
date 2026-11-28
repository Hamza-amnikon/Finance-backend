using System.Net;
using SparkFinance.Http;
using SalesInvoiceService.API.DTOs;

namespace SalesInvoiceService.API.Services
{
    public class EstimateApiService : IEstimateApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _context;

        public EstimateApiService(HttpClient httpClient, IHttpContextAccessor context)
        {
            _httpClient = httpClient;
            _context = context;
        }

        public async Task<EstimateForInvoiceDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/sales-estimates/{id}");
            FinanceSessionForwarding.AttachTo(request, _context.HttpContext?.Request);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

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
