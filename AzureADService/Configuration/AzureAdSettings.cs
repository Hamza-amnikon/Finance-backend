namespace AzureADService.Configuration
{
    public class AzureAdSettings
    {
        public Guid TenantId { get; set; }

        public Guid ClientId { get; set; }

        public string ClientSecret { get; set; } = string.Empty;

        public bool IsConfigured => TenantId != Guid.Empty &&
            ClientId != Guid.Empty && !string.IsNullOrWhiteSpace(ClientSecret);
    }
}
