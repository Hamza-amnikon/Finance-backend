namespace RoleService.API.DTOs
{
    public sealed class UserPagePermissionResponse
    {
        public string PermissionCode { get; set; } = string.Empty;

        public string Module { get; set; } = string.Empty;

        public string Route { get; set; } = string.Empty;

        public int? CompanyId { get; set; }

        public bool CanView { get; set; }

        public bool CanCreate { get; set; }

        public bool CanEdit { get; set; }
    }
}
