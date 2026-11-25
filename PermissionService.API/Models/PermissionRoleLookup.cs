namespace PermissionService.API.Models
{
    public sealed class PermissionRoleLookup
    {
        public int RoleId { get; set; }
        public string Role { get; set; } = "";
        public string Status { get; set; } = "";
    }
}
