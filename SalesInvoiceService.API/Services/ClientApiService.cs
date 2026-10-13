using System.Net;
using System.Net.Http.Json;
using SalesInvoiceService.API.DTOs;

namespace SalesInvoiceService.API.Services
{
    public class ClientApiService : IClientApiService
    {
        private readonly HttpClient _httpClient;

        public ClientApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<InvoiceClientDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync(
                $"api/clients/{id}",
                cancellationToken);

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