using System.Net.Http.Json;
using System.Text.Json;
using BankingService.API.DTOs;

namespace BankingService.API.Services
{
    public class PaymentAccountingApiService
        : IPaymentAccountingApiService
    {
        private readonly HttpClient _httpClient;

        public PaymentAccountingApiService(
            HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<PaymentReceivedAccountingResultDto>
            PostPaymentReceivedAsync(
                PaymentReceivedAccountingPostingDto posting,
                CancellationToken cancellationToken)
        {
            using var response =
                await _httpClient.PostAsJsonAsync(
                    "api/accounting-postings/payments-received",
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
                    PaymentReceivedAccountingResultDto>(
                    cancellationToken:
                        cancellationToken)
                ?? throw new HttpRequestException(
                    "AccountingService returned an empty response.");
        }

        public async Task<PaymentMadeAccountingResultDto>
            PostPaymentMadeAsync(
                PaymentMadeAccountingPostingDto posting,
                CancellationToken cancellationToken)
        {
            using var response =
                await _httpClient.PostAsJsonAsync(
                    "api/accounting-postings/payments-made",
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
                    PaymentMadeAccountingResultDto>(
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
                    "AccountingService rejected the payment posting.";
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
