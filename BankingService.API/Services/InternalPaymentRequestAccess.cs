using System.Net.Http.Json;
using BankingService.API.Models;
using SparkFinance.Http;

namespace BankingService.API.Services
{
    public sealed record InternalPaymentRequestScope(
        bool IsAdmin,
        int[] CompanyIds,
        int[] CreatableCompanyIds,
        int[] EditableCompanyIds,
        int[] ApprovableCompanyIds,
        string ActorId)
    {
        public int[] CancellableCompanyIds { get; init; } = [];

        public bool IsCreator(InternalPaymentRequest request)
        {
            return !string.IsNullOrWhiteSpace(ActorId) &&
                string.Equals(ActorId, request.CreatedBy, StringComparison.OrdinalIgnoreCase);
        }

        public bool CanCancel(int companyId)
        {
            return CanView(companyId) && (IsAdmin || CancellableCompanyIds.Contains(companyId));
        }

        public int[] ExportableCompanyIds { get; init; } = [];

        public bool CanExport(int companyId)
        {
            return CanView(companyId) && (IsAdmin || ExportableCompanyIds.Contains(companyId));
        }

        public bool CanView(int companyId)
        {
            return companyId > 0 && (IsAdmin || CompanyIds.Contains(companyId));
        }

        public bool CanCreate(int companyId)
        {
            return CanView(companyId) && (IsAdmin || CreatableCompanyIds.Contains(companyId));
        }

        public bool CanEdit(int companyId)
        {
            return CanView(companyId) && (IsAdmin || EditableCompanyIds.Contains(companyId));
        }

        public bool CanApprove(int companyId)
        {
            return CanView(companyId) && (IsAdmin || ApprovableCompanyIds.Contains(companyId));
        }
    }

    public sealed class InternalPaymentRequestAccess(
        HttpClient client,
        IConfiguration configuration,
        ILogger<InternalPaymentRequestAccess> logger)
    {
        public const string Route = "/banking/internal-payment-requests";

        public async Task<InternalPaymentRequestScope> GetAsync(
            HttpRequest request,
            CancellationToken cancellationToken,
            string route = Route)
        {
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Get, FinanceCompanyAccess.CreateEndpoint(configuration, route));
                FinanceSessionForwarding.AttachTo(message, request);
                using var response = await client.SendAsync(message, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InternalPaymentRequestException(
                        response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                            ? (int)response.StatusCode : 503,
                        "Internal payment request access could not be verified.");
                }

                var scope = await response.Content.ReadFromJsonAsync<InternalPaymentRequestScope>(cancellationToken);
                if (scope == null || string.IsNullOrWhiteSpace(scope.ActorId) || scope.CompanyIds == null ||
                    scope.CreatableCompanyIds == null || scope.EditableCompanyIds == null || scope.ApprovableCompanyIds == null)
                {
                    throw new InvalidOperationException("The verified company scope is incomplete.");
                }
                return scope;
            }
            catch (InternalPaymentRequestException)
            {
                throw;
            }
            catch (Exception exception) when (!RequestCancellation.IsCancellation(exception, cancellationToken))
            {
                logger.LogWarning(exception, "Internal payment request access verification failed.");
                throw new InternalPaymentRequestException(503, "Company access could not be verified. Please retry.");
            }
        }
    }

    public sealed class InternalPaymentRequestException(int status, string message) : Exception(message)
    {
        public int Status { get; } = status;
    }
}
