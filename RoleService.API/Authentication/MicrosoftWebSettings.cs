namespace RoleService.API.Authentication
{
    public class MicrosoftWebSettings
    {
        public bool Enabled { get; set; }

        public string FrontendUrl { get; set; } = string.Empty;

        public string CallbackPath { get; set; } = "/signin-oidc";
    }
}
