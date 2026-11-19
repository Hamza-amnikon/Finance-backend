using SparkFinance.Http;
using System.Net;
using System.Net.Http.Json;
using SalesInvoiceService.API.DTOs;

namespace SalesInvoiceService.API.Services
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

        public async Task<InvoiceClientDto?> GetByIdAsync(
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

            return await response.Content
                .ReadFromJsonAsync<InvoiceClientDto>(
                    cancellationToken: cancellationToken)
                ?? throw new HttpRequestException(
                    "ClientService returned an empty client response.");
        }
    }
}
