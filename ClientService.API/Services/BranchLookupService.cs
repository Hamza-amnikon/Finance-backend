using System.Net.Http.Json;
using System.Text.Json;

namespace ClientService.API.Services
{
    public class BranchLookupService : IBranchLookupService
    {
        private readonly HttpClient _httpClient;

        public BranchLookupService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string?> GetActiveNameAsync(
            string name,
            CancellationToken cancellationToken)
        {
            var value = name?.Trim() ?? string.Empty;

            if (value.Length == 0 || value.Length > 20)
            {
                return null;
            }

            List<BranchOption>? branches;

            try
            {
                branches = await _httpClient.GetFromJsonAsync<List<BranchOption>>(
                    "api/branches",
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new HttpRequestException(
                    "The branch API returned an invalid response.",
                    exception);
            }

            if (branches == null ||
                branches.Any(branch => branch == null ||
                    string.IsNullOrWhiteSpace(branch.Name)))
            {
                throw new HttpRequestException(
                    "The branch API returned an invalid response.");
            }

            return branches.FirstOrDefault(branch =>
                branch.IsActive &&
                string.Equals(
                    branch.Name,
                    value,
                    StringComparison.OrdinalIgnoreCase))?.Name;
        }

        private sealed class BranchOption
        {
            public BranchOption()
            {
            }

            public string Name { get; set; } = string.Empty;

            public bool IsActive { get; set; }
        }
    }
}
