namespace PermissionService.API.Authentication
{
    public class LoginSessionSettings
    {
        public string MicrosoftSessionCookieName { get; set; } = "Finance.Jwt";

        public bool UseDevelopmentSession { get; set; }

        public string SessionCookieName { get; set; } = string.Empty;

        public string[] ManageRolePermissionsRoles { get; set; } = [];
    }
}
