using System.Net;
using System.Net.Http.Json;
using SalesEstimateService.API.DTOs;

namespace SalesEstimateService.API.Services
{
    public class ClientApiService : IClientApiService
    {
        private readonly HttpClient _httpClient;

        public ClientApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<EstimateClientDto?> GetByIdAsync(
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