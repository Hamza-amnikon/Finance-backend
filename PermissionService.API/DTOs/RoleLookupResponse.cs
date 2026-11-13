namespace PermissionService.API.DTOs
{
    public class RoleLookupResponse
    {
        public int RoleId { get; set; }

        public string Role { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }
}