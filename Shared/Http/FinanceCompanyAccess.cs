using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SparkFinance.Http
{
    public sealed class FinanceCompanyAccess(
        HttpClient client,
        IConfiguration configuration,
        ILogger<FinanceCompanyAccess> logger)
    {
        public async Task<FinanceCompanyScope> GetAsync(
            HttpRequest request,
            string route,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!Uri.TryCreate(configuration["Services:AccountingService:BaseUrl"], UriKind.Absolute, out var baseUrl) ||
                    baseUrl.Scheme != Uri.UriSchemeHttps)
                {
                    throw new InvalidOperationException("Configure the trusted HTTPS Accounting Service URL.");
                }

                var url = new Uri(baseUrl, "/api/accounts/company-access?route=" + Uri.EscapeDataString(route));
                using var message = new HttpRequestMessage(HttpMethod.Get, url);
                FinanceSessionForwarding.AttachTo(message, request);

                using var response = await client.SendAsync(message, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var status = (int)response.StatusCode;
                    return new FinanceCompanyScope(status is 401 or 403 ? status : 503, null);
                }

                var payload = await response.Content.ReadFromJsonAsync<CompanyAccessResponse>(cancellationToken)
                    ?? throw new HttpRequestException("Accounting Service returned no company scope.");

                return new FinanceCompanyScope(
                    200,
                    payload.IsAdmin ? null : payload.Branches.Select(branch => branch.Name).ToArray())
                {
                    CreatableBranches = MapBranches(payload, payload.CreatableCompanyIds),
                    EditableBranches = MapBranches(payload, payload.EditableCompanyIds),
                    DeletableBranches = MapBranches(payload, payload.DeletableCompanyIds),
                    IssuableBranches = MapBranches(payload, payload.IssuableCompanyIds)
                };
            }
            catch (Exception exception) when (!RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                logger.LogWarning(exception, "Company scope could not be loaded for {Route}.", route);
                return new FinanceCompanyScope(503, null);
            }
        }

        private static string[] MapBranches(CompanyAccessResponse scope, int[] companyIds)
        {
            return scope.Branches.Where(branch => branch.AccountingCompanyId is int id && companyIds.Contains(id))
                .Select(branch => branch.Name)
                .ToArray();
        }

        private sealed record CompanyAccessResponse(
            bool IsAdmin,
            Branch[] Branches,
            int[] CreatableCompanyIds,
            int[] EditableCompanyIds,
            int[] DeletableCompanyIds,
            int[] IssuableCompanyIds);

        private sealed record Branch(string Name, int? AccountingCompanyId);
    }

    public sealed record FinanceCompanyScope(int Status, string[]? Branches)
    {
        public string[] CreatableBranches { get; init; } = [];
        public string[] EditableBranches { get; init; } = [];
        public string[] DeletableBranches { get; init; } = [];
        public string[] IssuableBranches { get; init; } = [];

        public bool AllowsAction(string? branch, string[] actionBranches)
        {
            return Allows(branch) && (Branches == null || actionBranches.Contains(branch, StringComparer.OrdinalIgnoreCase));
        }

        public bool Allows(string? branch)
        {
            return Status == 200 && (Branches == null || Branches.Contains(branch, StringComparer.OrdinalIgnoreCase));
        }
    }
}
