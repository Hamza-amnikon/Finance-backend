namespace PermissionService.API.Authentication
{
    public class LoginSessionSettings
    {
        public bool UseDevelopmentSession { get; set; }

        public string SessionCookieName { get; set; } = string.Empty;

        public string[] ManageRolePermissionsRoles { get; set; } = [];
    }
}
