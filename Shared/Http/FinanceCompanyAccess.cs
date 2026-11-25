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
            if (cancellationToken.IsCancellationRequested)
            {
                return new FinanceCompanyScope(499, []);
            }

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
                    CompanyIds = payload.IsAdmin ? null : payload.CompanyIds ?? [],
                    IsAdmin = payload.IsAdmin,
                    ActorId = payload.ActorId,
                    ApprovableCompanyIds = payload.ApprovableCompanyIds ?? [],
                    IssuableCompanyIds = payload.IssuableCompanyIds ?? [],
                    CreatableCompanyIds = payload.CreatableCompanyIds ?? [],
                    EditableCompanyIds = payload.EditableCompanyIds ?? [],
                    CancellableCompanyIds = payload.CancellableCompanyIds ?? [],
                    PostableCompanyIds = payload.PostableCompanyIds ?? [],
                    ReconcilableCompanyIds = payload.ReconcilableCompanyIds ?? [],
                    VoidableCompanyIds = payload.VoidableCompanyIds ?? [],
                    CreatableBranches = MapBranches(payload, payload.CreatableCompanyIds),
                    EditableBranches = MapBranches(payload, payload.EditableCompanyIds),
                    DeletableBranches = MapBranches(payload, payload.DeletableCompanyIds),
                    IssuableBranches = MapBranches(payload, payload.IssuableCompanyIds),
                    ApprovableBranches = MapBranches(payload, payload.ApprovableCompanyIds),
                    CancellableBranches = MapBranches(payload, payload.CancellableCompanyIds),
                    ExportableBranches = MapBranches(payload, payload.ExportableCompanyIds),
                    PostableBranches = MapBranches(payload, payload.PostableCompanyIds),
                    VoidableBranches = MapBranches(payload, payload.VoidableCompanyIds),
                    ReversibleBranches = MapBranches(payload, payload.ReversibleCompanyIds),
                    ReconcilableBranches = MapBranches(payload, payload.ReconcilableCompanyIds)
                };
            }
            catch (OperationCanceledException exception)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new FinanceCompanyScope(499, []);
                }

                logger.LogWarning(
                    exception,
                    "Company access verification timed out for {Route}.",
                    route);

                return new FinanceCompanyScope(503, []);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Company scope could not be loaded for {Route}.", route);
                return new FinanceCompanyScope(503, []);
            }
        }

        private static string[] MapBranches(CompanyAccessResponse scope, int[]? companyIds)
        {
            if (companyIds == null)
            {
                return [];
            }

            return scope.Branches.Where(branch => branch.AccountingCompanyId is int id && companyIds.Contains(id))
                .Select(branch => branch.Name)
                .ToArray();
        }

        private sealed record CompanyAccessResponse(
            bool IsAdmin,
            Branch[] Branches,
            int[] CompanyIds,
            int[] CreatableCompanyIds,
            int[] EditableCompanyIds,
            int[] DeletableCompanyIds,
            int[] IssuableCompanyIds,
            int[] ApprovableCompanyIds,
            int[] CancellableCompanyIds,
            int[] ExportableCompanyIds,
            int[]? PostableCompanyIds,
            int[]? VoidableCompanyIds,
            int[]? ReversibleCompanyIds,
            int[]? ReconcilableCompanyIds,
            string? ActorId = null);

        private sealed record Branch(string Name, int? AccountingCompanyId);
    }

    public sealed record FinanceCompanyScope(int Status, string[]? Branches)
    {
        public int[]? CompanyIds { get; init; } = [];
        public bool IsAdmin { get; init; }
        public string? ActorId { get; init; }
        public int[] ApprovableCompanyIds { get; init; } = [];
        public int[] IssuableCompanyIds { get; init; } = [];
        public int[] CreatableCompanyIds { get; init; } = [];
        public int[] EditableCompanyIds { get; init; } = [];
        public int[] CancellableCompanyIds { get; init; } = [];
        public int[] PostableCompanyIds { get; init; } = [];
        public int[] ReconcilableCompanyIds { get; init; } = [];
        public int[] VoidableCompanyIds { get; init; } = [];
        public string[] CreatableBranches { get; init; } = [];
        public string[] EditableBranches { get; init; } = [];
        public string[] DeletableBranches { get; init; } = [];
        public string[] IssuableBranches { get; init; } = [];

        public string[] ApprovableBranches { get; init; } = [];

        public string[] CancellableBranches { get; init; } = [];

        public string[] ExportableBranches { get; init; } = [];

        public string[] PostableBranches { get; init; } = [];

        public string[] VoidableBranches { get; init; } = [];

        public string[] ReversibleBranches { get; init; } = [];

        public string[] ReconcilableBranches { get; init; } = [];

        public bool AllowsAction(string? branch, string[] actionBranches)
        {
            return Allows(branch) && (Branches == null || actionBranches.Contains(branch, StringComparer.OrdinalIgnoreCase));
        }

        public bool AllowsCompany(int companyId)
        {
            return Status == 200 && companyId > 0 &&
                (CompanyIds == null || CompanyIds.Contains(companyId));
        }

        public bool AllowsCompanyAction(int companyId, int[] actionCompanyIds)
        {
            return AllowsCompany(companyId) &&
                (CompanyIds == null || actionCompanyIds.Contains(companyId));
        }

        public bool Allows(string? branch)
        {
            return Status == 200 && (Branches == null || Branches.Contains(branch, StringComparer.OrdinalIgnoreCase));
        }
    }
}
