using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using AccountingService.API.Authentication;
using AccountingService.API.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using SparkFinance.Authentication;

namespace AccountingService.API.Services
{
    public interface IGeneralLedgerAccess
    {
        Task<LedgerAccess> GetAsync(HttpRequest request, CancellationToken cancellationToken);
    }

    public sealed record LedgerAccess(int Status, bool IsAdmin, IReadOnlyList<int> CompanyIds)
    {
        public bool Allows(int companyId)
        {
            return Status == 200 && companyId > 0 && (IsAdmin || CompanyIds.Contains(companyId));
        }
    }

    public sealed class GeneralLedgerAccess : IGeneralLedgerAccess
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

        public async Task<LedgerAccess> GetAsync(HttpRequest request, CancellationToken cancellationToken)
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

            // The identity service validates active users and roles. Never accept a role from the browser.
            var code = "accounting.general-ledger";
            var companies = await _context.Database.SqlQuery<int>($"""
                SELECT DISTINCT rp.CompanyId AS Value
                FROM security.RolePermissions rp
                INNER JOIN security.Permissions p ON p.PermissionId = rp.PermissionId
                WHERE rp.RoleId = {session.RoleId} AND rp.CompanyId IS NOT NULL
                    AND rp.CanView = 1 AND p.Status = 'Active' AND p.PermissionCode = {code}
                """).ToListAsync(cancellationToken);

            return new LedgerAccess(200, false, companies);
        }

        private sealed class VerifiedSession
        {
            public Guid UserId { get; set; }
            public int RoleId { get; set; }
            public string Role { get; set; } = string.Empty;
        }
    }
}
