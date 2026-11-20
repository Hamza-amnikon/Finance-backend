using DashboardService.API.Data;
using Microsoft.EntityFrameworkCore;
using SparkFinance.Http;
using System.Text.Json;

namespace DashboardService.API.Services
{
    public sealed record DashboardCompany(int Id, string Currency);

    public sealed record DashboardBranch(int BranchId, int CompanyId, string BranchName, string Currency);

    public sealed class DashboardAccessService(
        HttpClient client,
        IConfiguration configuration,
        DashboardDbContext context)
    {
        public async Task<(int Status, IReadOnlyList<DashboardBranch> Branches)> GetAsync(
            HttpRequest request,
            CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(configuration["DashboardAccess:CompaniesUrl"], UriKind.Absolute, out var url) ||
                url.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException("Configure the HTTPS Dashboard company verification URL.");
            }

            using var message = new HttpRequestMessage(HttpMethod.Get, url);
            FinanceSessionForwarding.AttachTo(message, request);

            using var response = await client.SendAsync(message, cancellationToken);
            var status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                return (status is 401 or 403 ? status : 503, []);
            }

            var companies = await response.Content.ReadFromJsonAsync<List<DashboardCompany>>(cancellationToken)
                ?? throw new JsonException("Dashboard companies are missing.");

            if (companies.Count == 0)
            {
                return (403, []);
            }

            var companyIds = JsonSerializer.Serialize(companies.Select(company => company.Id));
            var branches = await context.Database.SqlQuery<DashboardBranchRow>($"""
                SELECT b.Id AS BranchId, b.AccountingCompanyId AS CompanyId, b.Name AS BranchName
                FROM amnikon.Branches b
                WHERE b.IsActive = 1
                    AND b.AccountingCompanyId IN (SELECT value FROM OPENJSON({companyIds}) WITH (value int '$'))
                    AND NOT EXISTS (
                        SELECT 1 FROM amnikon.Branches other
                        WHERE other.Name = b.Name AND other.Id <> b.Id
                    )
                ORDER BY b.Name
                """).ToListAsync(cancellationToken);

            var currencies = companies.ToDictionary(company => company.Id, company => company.Currency);
            return (200, branches.Select(branch => new DashboardBranch(
                branch.BranchId,
                branch.CompanyId,
                branch.BranchName,
                currencies[branch.CompanyId])).ToArray());
        }

        public sealed class DashboardBranchRow
        {
            public int BranchId { get; set; }
            public int CompanyId { get; set; }
            public string BranchName { get; set; } = string.Empty;
        }
    }
}
