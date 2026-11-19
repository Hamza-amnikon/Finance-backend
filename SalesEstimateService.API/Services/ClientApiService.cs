using SparkFinance.Http;
using System.Net;
using System.Net.Http.Json;
using SalesEstimateService.API.DTOs;

namespace SalesEstimateService.API.Services
{
    public class ClientApiService : IClientApiService
    {
        private readonly HttpClient _httpClient;

        private readonly IHttpContextAccessor? _context;

        public ClientApiService(HttpClient httpClient, IHttpContextAccessor? context = null)
        {
_httpClient = httpClient;
            _context = context;
        }

        public async Task<EstimateClientDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/clients/{id}");
            FinanceSessionForwarding.AttachTo(request, _context?.HttpContext?.Request);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            try
            {
                return await response.Content
                    .ReadFromJsonAsync<EstimateClientDto>(
                        cancellationToken: cancellationToken)
                    ?? throw new HttpRequestException(
                        "ClientService returned an empty client response.");
            }
            catch (System.Text.Json.JsonException exception)
            {
                throw new HttpRequestException(
                    "ClientService returned an invalid client response.",
                    exception);
            }
        }
    }
}
