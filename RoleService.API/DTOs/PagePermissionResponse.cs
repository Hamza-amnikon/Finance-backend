namespace RoleService.API.DTOs
{
    public sealed class PagePermissionResponse
    {
        public string Route { get; init; } = string.Empty;
        public int CompanyId { get; init; }
        public bool CanView { get; init; }
    }
}
