using System.Net.Http.Json;
using System.Text.Json;
using BillService.API.DTOs;

namespace BillService.API.Services
{
    public class BillAccountingApiService : IBillAccountingApiService
    {
        private readonly HttpClient _httpClient;

        public BillAccountingApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<BillAccountingCompanyDto?> GetCompanyAsync(
            int companyId,
            CancellationToken cancellationToken)
        {
            var companies = await _httpClient.GetFromJsonAsync<List<BillAccountingCompanyDto>>(
                "api/accounts/lookups/companies",
                cancellationToken)
                ?? throw new HttpRequestException("AccountingService returned an empty company response.");

            return companies.FirstOrDefault(company => company.Id == companyId);
        }

        public async Task<BillAccountingResultDto> PostBillAsync(
            BillAccountingPostingDto posting,
            CancellationToken cancellationToken)
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/accounting-postings/purchase-bills",
                posting,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var message = "AccountingService rejected the purchase bill posting.";

                try
                {
                    var error = await response.Content.ReadFromJsonAsync<BillAccountingErrorDto>(
                        cancellationToken: cancellationToken);
                    message = error?.Message ?? error?.Detail ?? error?.Title ?? message;
                }
                catch (JsonException)
                {
                }

                throw new HttpRequestException(message, null, response.StatusCode);
            }

            return await response.Content.ReadFromJsonAsync<BillAccountingResultDto>(
                cancellationToken: cancellationToken)
                ?? throw new HttpRequestException("AccountingService returned an empty response.");
        }

        public async Task<BillAccountingResultDto> ReverseBillAsync(int billId, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.PostAsync(
                $"api/accounting-postings/purchase-bills/{billId}/reverse", null, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException("The bill is cancelled, but its accounting reversal is pending. Retry cancellation to complete it.",
                    null, response.StatusCode);
            }
            var result = await response.Content.ReadFromJsonAsync<BillAccountingResultDto>(cancellationToken: cancellationToken)
                ?? throw new HttpRequestException("Accounting returned no cancellation confirmation.");
            if (result.Status != "Reversed" && result.Status != "NoPosting")
            {
                throw new HttpRequestException("Accounting did not confirm the bill cancellation reversal.");
            }
            return result;
        }

        private sealed class BillAccountingErrorDto
        {
            public string? Message { get; set; }
            public string? Detail { get; set; }
            public string? Title { get; set; }
        }
    }
}
