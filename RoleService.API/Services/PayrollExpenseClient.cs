using System.Net;
using System.Text;
using System.Text.Json;
using RoleService.API.DTOs;

namespace RoleService.API.Services;

public sealed class PayrollExpenseClient(HttpClient http, IConfiguration configuration)
{
    public async Task<(HttpStatusCode Status, PayrollExpensePreviewDto? Preview)> GetSummaryAsync(
        int companyId, int year, int month, CancellationToken cancellationToken)
    {
        var settings = configuration.GetSection("PayrollExpense");
        var mappings = settings.GetSection("CompanyGroups");
        var group = mappings[companyId.ToString(System.Globalization.CultureInfo.InvariantCulture)]?.Trim();
        if (string.IsNullOrWhiteSpace(group))
        {
            return (HttpStatusCode.NotFound, null);
        }
        if (mappings.GetChildren().Count(mapping =>
                string.Equals(mapping.Value?.Trim(), group, StringComparison.OrdinalIgnoreCase)) != 1)
        {
            return (HttpStatusCode.ServiceUnavailable, null);
        }

        var key = settings["ServiceKey"];
        if (!Uri.TryCreate(settings["BaseUrl"], UriKind.Absolute, out var baseUrl) ||
            baseUrl.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(baseUrl.UserInfo) ||
            !string.IsNullOrEmpty(baseUrl.Query) || !string.IsNullOrEmpty(baseUrl.Fragment) ||
            !baseUrl.AbsolutePath.EndsWith('/') ||
            string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32 ||
            key.Length > 512 || key.Any(c => c < '!' || c > '~'))
        {
            return (HttpStatusCode.ServiceUnavailable, null);
        }

        var query = FormattableString.Invariant(
            $"api/payroll/finance-expense/summary?group={Uri.EscapeDataString(group)}&year={year}&month={month}");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUrl, query));
        request.Headers.Add("X-Finance-Payroll-Key", key);
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict)
            {
                return (response.StatusCode, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                return (HttpStatusCode.BadGateway, null);
            }

            var data = await response.Content.ReadFromJsonAsync<PayrollExpensePreviewDto>(cancellationToken);
            if (data is null || data.PayrollRunId <= 0 || data.Year != year || data.Month != month ||
                data.NetSalary <= 0 || data.Currency is not { Length: 3 } ||
                data.Currency.Any(c => c is < 'A' or > 'Z'))
            {
                return (HttpStatusCode.BadGateway, null);
            }

            return (HttpStatusCode.OK, data);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (HttpStatusCode.GatewayTimeout, null);
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or NotSupportedException)
        {
            return (HttpStatusCode.BadGateway, null);
        }
    }
}
