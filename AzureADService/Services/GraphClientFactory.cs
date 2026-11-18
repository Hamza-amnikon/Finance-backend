using Azure.Identity;
using AzureADService.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace AzureADService.Services
{
    public class GraphClientFactory
    {
        public GraphServiceClient? Client { get; }

        public Guid TenantId { get; }

        public GraphClientFactory(IOptions<AzureAdSettings> options)
        {
            var settings = options.Value;
            TenantId = settings.TenantId;

            if (settings.IsConfigured)
            {
                var credential = new ClientSecretCredential(
                    settings.TenantId.ToString(),
                    settings.ClientId.ToString(),
                    settings.ClientSecret);

                Client = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
            }
        }
    }
}
