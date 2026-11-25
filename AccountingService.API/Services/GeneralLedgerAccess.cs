using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using AccountingService.API.Authentication;
using AccountingService.API.Data;
using AccountingService.API.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using SparkFinance.Authentication;

namespace AccountingService.API.Services
{
    public interface IGeneralLedgerAccess
    {
        Task<LedgerAccess> GetAsync(HttpRequest request, CancellationToken cancellationToken);
    }

    public interface IAccountingPageAccess
    {
        Task<LedgerAccess> GetAsync(
            HttpRequest request,
            string? permissionCode,
            CancellationToken cancellationToken);
    }

    public sealed record LedgerAccess(int Status, bool IsAdmin, IReadOnlyList<int> CompanyIds)
    {
        public IReadOnlyList<int> CreatableCompanyIds { get; init; } = [];

        public IReadOnlyList<int> EditableCompanyIds { get; init; } = [];

        public IReadOnlyList<int> DeletableCompanyIds { get; init; } = [];

        public IReadOnlyList<int> ExportableCompanyIds { get; init; } = [];

        public IReadOnlyList<int> CancellableCompanyIds { get; init; } = [];

        public IReadOnlyList<int> ApprovableCompanyIds { get; init; } = [];

        public IReadOnlyList<int> IssuableCompanyIds { get; init; } = [];

        public IReadOnlyList<int> PostableCompanyIds { get; init; } = [];

        public IReadOnlyList<int> VoidableCompanyIds { get; init; } = [];

        public IReadOnlyList<int> ReversibleCompanyIds { get; init; } = [];

        public IReadOnlyList<int> ReconcilableCompanyIds { get; init; } = [];

        public bool Allows(int companyId)
        {
            return Status == 200 && companyId > 0 && (IsAdmin || CompanyIds.Contains(companyId));
        }

        public bool AllowsCreate(int companyId)
        {
            return Allows(companyId) && (IsAdmin || CreatableCompanyIds.Contains(companyId));
        }

        public bool AllowsEdit(int companyId)
        {
            return Allows(companyId) && (IsAdmin || EditableCompanyIds.Contains(companyId));
        }
    }

    public sealed class GeneralLedgerAccess : IGeneralLedgerAccess, IAccountingPageAccess
    {
        private readonly HttpClient _client;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;
        private readonly AccountingDbContext _context;

        public GeneralLedgerAccess(
            HttpClient client,
            IConfiguration configuration,
            IHostEnvironment environment,
            AccountingDbContext context)
        {
            _client = client;
            _configuration = configuration;
            _environment = environment;
            _context = context;
        }

        public Task<LedgerAccess> GetAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            return GetAsync(request, "accounting.general-ledger", cancellationToken);
        }

        public async Task<LedgerAccess> GetAsync(
            HttpRequest request,
            string? permissionCode,
            CancellationToken cancellationToken)
        {
            var authorization = request.Headers.Authorization.ToString();
            var cookieName = _configuration.GetSection("JwtValidation")
                .Get<JwtValidationSettings>()?.CookieName;
            var microsoftCookie = string.IsNullOrWhiteSpace(cookieName)
                ? null
                : request.Cookies[cookieName];
            var adminCookie = request.Cookies["Finance.DevelopmentAdmin"];
            var requestedMethod = request.Headers["X-Finance-Authentication"].ToString();
            if (requestedMethod.Length > 0 && requestedMethod is not ("admin" or "microsoft"))
            {
                return new LedgerAccess(401, false, []);
            }
            // This selects which server verifies credentials; it never grants a role or access.
            var microsoft = requestedMethod == "microsoft" ||
                (requestedMethod != "admin" &&
                    (!string.IsNullOrWhiteSpace(authorization) || !string.IsNullOrWhiteSpace(microsoftCookie)));

            if (!microsoft && (!_environment.IsDevelopment() || string.IsNullOrWhiteSpace(adminCookie)))
            {
                return new LedgerAccess(401, false, []);
            }

            ClaimsPrincipal? jwtPrincipal = null;

            if (microsoft)
            {
                var authentication = await request.HttpContext.AuthenticateAsync(
                    AccountingAuthenticationSchemes.MeritJwt);

                if (!authentication.Succeeded || authentication.Principal == null)
                {
                    return new LedgerAccess(401, false, []);
                }

                jwtPrincipal = authentication.Principal;
            }

            var setting = microsoft ? "AuthSessionUrl" : "AdminSessionUrl";
            if (!Uri.TryCreate(_configuration["GeneralLedgerAccess:" + setting], UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new HttpRequestException("Configure the HTTPS session verification URL.");
            }

            using var message = new HttpRequestMessage(HttpMethod.Get, uri);
            if (microsoft && !string.IsNullOrWhiteSpace(authorization))
            {
                if (!AuthenticationHeaderValue.TryParse(authorization, out var header) ||
                    !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
                {
                    return new LedgerAccess(401, false, []);
                }
                message.Headers.Authorization = header;
            }
            else
            {
                var sessionCookieName = microsoft ? cookieName : "Finance.DevelopmentAdmin";
                message.Headers.Add("Cookie", sessionCookieName + "=" + (microsoft ? microsoftCookie : adminCookie));
            }

            using var response = await _client.SendAsync(message, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new LedgerAccess((int)response.StatusCode, false, []);
            }
            response.EnsureSuccessStatusCode();
            var session = await response.Content.ReadFromJsonAsync<VerifiedSession>(cancellationToken);
            if (session == null)
            {
                throw new HttpRequestException("Session verification returned no identity.");
            }

            if (!microsoft)
            {
                if (session.Role == "Admin")
                {
                    request.HttpContext.User = CreateSessionPrincipal(session, "admin");
                }

                return session.Role == "Admin"
                    ? new LedgerAccess(200, true, [])
                    : new LedgerAccess(403, false, []);
            }

            if (session.UserId == Guid.Empty || session.RoleId <= 0 ||
                !Guid.TryParse(jwtPrincipal?.FindFirstValue("sub"), out var tokenUserId) ||
                tokenUserId != session.UserId)
            {
                return new LedgerAccess(401, false, []);
            }

            request.HttpContext.User = CreateSessionPrincipal(session, "microsoft");

            if (session.IsAdmin)
            {
                return new LedgerAccess(200, true, []);
            }

            // The identity service validates active users and roles. Never accept a role from the browser.
            var companies = await (
                from grant in _context.Set<LedgerCompanyGrantLookup>().AsNoTracking()
                join permission in _context.Set<LedgerPagePermissionLookup>().AsNoTracking()
                    on grant.PermissionId equals permission.PermissionId
                where grant.RoleId == session.RoleId && grant.CompanyId > 0 && grant.CanView &&
                    permission.Status == "Active" &&
                    (permissionCode == null || permission.PermissionCode == permissionCode)
                select new CompanyPermission
                {
                    CompanyId = grant.CompanyId!.Value,
                    CanCreate = grant.CanCreate,
                    CanEdit = grant.CanEdit,
                    CanDelete = grant.CanDelete,
                    CanIssue = grant.CanIssue,
                    CanApprove = grant.CanApprove,
                    CanExport = grant.CanExport,
                    CanCancel = grant.CanCancel,
                    CanPost = grant.CanPost,
                    CanVoid = grant.CanVoid,
                    CanReverse = grant.CanReverse,
                    CanReconcile = grant.CanReconcile
                }
            ).Distinct().ToListAsync(cancellationToken);

            return new LedgerAccess(200, false, companies.Select(company => company.CompanyId).Distinct().ToArray())
            {
                CreatableCompanyIds = companies.Where(company => company.CanCreate)
                    .Select(company => company.CompanyId).Distinct().ToArray(),
                EditableCompanyIds = companies.Where(company => company.CanEdit)
                    .Select(company => company.CompanyId).Distinct().ToArray(),
                DeletableCompanyIds = companies.Where(company => company.CanDelete)
                    .Select(company => company.CompanyId).Distinct().ToArray(),
                ExportableCompanyIds = companies.Where(company => company.CanExport)
                    .Select(company => company.CompanyId).Distinct().ToArray(),
                CancellableCompanyIds = companies.Where(company => company.CanCancel)
                    .Select(company => company.CompanyId).Distinct().ToArray(),
                ApprovableCompanyIds = companies.Where(company => company.CanApprove)
                    .Select(company => company.CompanyId).Distinct().ToArray(),
                IssuableCompanyIds = companies.Where(company => company.CanIssue)
                    .Select(company => company.CompanyId).Distinct().ToArray(),
                PostableCompanyIds = companies.Where(company => company.CanPost)
                    .Select(company => company.CompanyId).Distinct().ToArray(),
                VoidableCompanyIds = companies.Where(company => company.CanVoid)
                    .Select(company => company.CompanyId).Distinct().ToArray(),
                ReversibleCompanyIds = companies.Where(company => company.CanReverse)
                    .Select(company => company.CompanyId).Distinct().ToArray(),
                ReconcilableCompanyIds = companies.Where(company => company.CanReconcile)
                    .Select(company => company.CompanyId).Distinct().ToArray()
            };
        }

        private static ClaimsPrincipal CreateSessionPrincipal(VerifiedSession session, string method)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, method == "admin" ? session.Name : session.UserId.ToString()),
                new Claim(ClaimTypes.Name, session.Name),
                new Claim(ClaimTypes.Role, session.Role)
            ], "VerifiedAccountingSession"));
        }

        private sealed class CompanyPermission
        {
            public int CompanyId { get; set; }
            public bool CanCreate { get; set; }
            public bool CanEdit { get; set; }
            public bool CanDelete { get; set; }
            public bool CanIssue { get; set; }
            public bool CanCancel { get; set; }
            public bool CanApprove { get; set; }
            public bool CanExport { get; set; }
            public bool CanPost { get; set; }
            public bool CanVoid { get; set; }
            public bool CanReverse { get; set; }
            public bool CanReconcile { get; set; }
        }

        private sealed class VerifiedSession
        {
            public bool IsAdmin { get; set; }
            public Guid UserId { get; set; }
            public int RoleId { get; set; }
            public string Role { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }
    }
}
