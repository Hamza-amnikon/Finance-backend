using SparkFinance.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SalesEstimateService.API.DTOs;

namespace SalesEstimateService.API.Services
{
    public class InvoiceApiService : IInvoiceApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor? _context;

        public InvoiceApiService(HttpClient httpClient, IHttpContextAccessor? context = null)
        {
_httpClient = httpClient;
            _context = context;
        }

        public async Task<ConvertedInvoiceDto> CreateFromEstimateAsync(
            int estimateId,
            ConvertSalesEstimateDto dto,
            CancellationToken cancellationToken
        )
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/sales-invoices/from-estimate")
            {
                Content = JsonContent.Create(new
                {
                    estimateId,
                    dto.RowVersion,
                    dto.InvoiceDate,
                    dto.DueDate,
                    dto.PreferredPaymentMode
                })
            };
            FinanceSessionForwarding.AttachTo(request, _context?.HttpContext?.Request);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Conflict)
            {
                var message = "Conversion was rejected. Reload the estimate and review its details.";
                try
                {
                    var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                    if (body.TryGetProperty("message", out var value) && value.ValueKind == JsonValueKind.String)
                    {
                        message = value.GetString() ?? message;
                    }
                }
                catch (JsonException)
                {
                }

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    throw new InvalidOperationException(message);
                }

                throw new ArgumentException(message);
            }

            response.EnsureSuccessStatusCode();
            return await ReadAsync(response, cancellationToken);
        }

        public async Task<ConvertedInvoiceDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken
        )
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/sales-invoices/{id}");
            FinanceSessionForwarding.AttachTo(request, _context?.HttpContext?.Request);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await ReadAsync(response, cancellationToken);
        }

        private static async Task<ConvertedInvoiceDto> ReadAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken
        )
        {
            try
            {
                return await response.Content.ReadFromJsonAsync<ConvertedInvoiceDto>(cancellationToken: cancellationToken) ?? throw new HttpRequestException("Invoice service returned an empty conversion response.");
            }
            catch (JsonException exception)
            {
                throw new HttpRequestException("Invoice service returned invalid conversion data.", exception);
            }
        }
    }
}
