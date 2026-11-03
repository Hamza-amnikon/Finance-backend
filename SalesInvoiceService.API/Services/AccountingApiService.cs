using System.Net.Http.Json;
using System.Text.Json;
using SalesInvoiceService.API.DTOs;

namespace SalesInvoiceService.API.Services
{
    public class AccountingApiService
        : IAccountingApiService
    {
        private readonly HttpClient _httpClient;

        public AccountingApiService(
            HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<AccountingCompanyDto?>
            GetCompanyAsync(
                int companyId,
                CancellationToken cancellationToken)
        {
            var companies =
                await _httpClient.GetFromJsonAsync<
                    List<AccountingCompanyDto>>(
                    "api/accounts/lookups/companies",
                    cancellationToken);

            if (companies == null)
            {
                throw new HttpRequestException(
                    "AccountingService returned an empty company response.");
            }

            return companies.FirstOrDefault(
                company =>
                    company.Id == companyId);
        }

        public async Task<SalesInvoiceAccountingResultDto>
            PostSalesInvoiceAsync(
                SalesInvoiceAccountingPostingDto posting,
                CancellationToken cancellationToken)
        {
            using var response =
                await _httpClient.PostAsJsonAsync(
                    "api/accounting-postings/sales-invoices",
                    posting,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error =
                    await ReadErrorAsync(
                        response,
                        cancellationToken);

                throw new HttpRequestException(
                    error,
                    null,
                    response.StatusCode);
            }

            return await response.Content
                .ReadFromJsonAsync<
                    SalesInvoiceAccountingResultDto>(
                    cancellationToken:
                        cancellationToken)
                ?? throw new HttpRequestException(
                    "AccountingService returned an empty response.");
        }

        private static async Task<string> ReadErrorAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            try
            {
                var content =
                    await response.Content
                        .ReadFromJsonAsync<
                            AccountingApiErrorDto>(
                            cancellationToken:
                                cancellationToken);

                return
                    content?.Message ??
                    content?.Detail ??
                    content?.Title ??
                    "AccountingService rejected the sales invoice posting.";
            }
            catch (JsonException)
            {
                return
                    "AccountingService returned an invalid error response.";
            }
        }

        private sealed class AccountingApiErrorDto
        {
            public string? Message { get; set; }

            public string? Title { get; set; }

            public string? Detail { get; set; }
        }
    }
}
