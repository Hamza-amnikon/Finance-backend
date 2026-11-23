using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PermissionService.API.DTOs;

namespace PermissionService.API.CallOtherApis
{
    public class LoginSessionClient : ILoginSessionClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _sessionCookieName;
        private readonly string _microsoftCookieName;

        public LoginSessionClient(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _microsoftCookieName = configuration["Authentication:MicrosoftSessionCookieName"] ?? "Finance.Jwt";

            var sessionCookieName =
                configuration["Authentication:SessionCookieName"];

            if (string.IsNullOrWhiteSpace(sessionCookieName))
            {
                throw new InvalidOperationException(
                    "Configure Authentication:SessionCookieName.");
            }

            _sessionCookieName = sessionCookieName;
        }

        public Task<LoginSessionResponse?> GetSessionAsync(
            string sessionCookieValue,
            CancellationToken cancellationToken)
        {
            return VerifyAsync(sessionCookieValue, false, cancellationToken);
        }

        public Task<LoginSessionResponse?> GetMicrosoftSessionAsync(
            string sessionCookieValue,
            CancellationToken cancellationToken)
        {
            return VerifyAsync(sessionCookieValue, true, cancellationToken);
        }

        private async Task<LoginSessionResponse?> VerifyAsync(
            string sessionCookieValue,
            bool microsoft,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionCookieValue))
            {
                return null;
            }

            if (sessionCookieValue.Contains(';') ||
                sessionCookieValue.Contains('\r') ||
                sessionCookieValue.Contains('\n'))
            {
                return null;
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                microsoft ? "api/auth/session" : "api/development-admin/session");

            request.Headers.Add(
                "Cookie",
                $"{(microsoft ? _microsoftCookieName : _sessionCookieName)}={sessionCookieValue}");

            using var response = await _httpClient.SendAsync(
                request,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            LoginSessionResponse? session;

            try
            {
                session = await response.Content
                    .ReadFromJsonAsync<LoginSessionResponse>(
                        cancellationToken: cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new HttpRequestException(
                    "Role Service returned an invalid login session response.",
                    exception);
            }

            if (session == null ||
                string.IsNullOrWhiteSpace(session.Name) ||
                string.IsNullOrWhiteSpace(session.Role))
            {
                throw new HttpRequestException(
                    "Role Service returned incomplete login session details.");
            }

            return session;
        }
    }
}