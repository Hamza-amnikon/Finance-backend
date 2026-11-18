namespace RoleService.API.Authentication
{
    public class EntraSettings
    {
        public string Instance { get; set; } = string.Empty;

        public Guid TenantId { get; set; }

        public Guid ClientId { get; set; }

        public string[] RequiredScopes { get; set; } = [];

        public string[] AllowedClientIds { get; set; } = [];
    }
}
