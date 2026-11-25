using System.Net.Http.Json;
using System.Text.Json;
using PermissionService.API.DTOs;
using SparkFinance.Http;

namespace PermissionService.API.CallOtherApis
{
    public class CompanyServiceClient : ICompanyServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContext;

        public CompanyServiceClient(
            HttpClient httpClient,
            IHttpContextAccessor httpContext)
        {
            _httpClient = httpClient;
            _httpContext = httpContext;
        }

        public async Task<CompanyLookupResponse?> GetByIdAsync(
            int companyId,
            CancellationToken cancellationToken)
        {
            if (companyId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(companyId),
                    "Select a valid company.");
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "api/expenses/lookups/company-setup/role-permissions");

            FinanceSessionForwarding.AttachTo(
                request,
                _httpContext.HttpContext?.Request);

            using var response = await _httpClient.SendAsync(
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            List<CompanyLookupResponse>? companies;

            try
            {
                companies = await response.Content
                    .ReadFromJsonAsync<List<CompanyLookupResponse>>(
                        cancellationToken: cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new HttpRequestException(
                    "Company Service returned an invalid response.",
                    exception);
            }

            if (companies == null ||
                companies.Any(company =>
                    company == null ||
                    company.CompanyId <= 0 ||
                    string.IsNullOrWhiteSpace(company.CompanyName)))
            {
                throw new HttpRequestException(
                    "Company Service returned incomplete or unexpected company details.");
            }

            return companies.FirstOrDefault(company =>
                company.CompanyId == companyId);
        }
    }
}
