namespace RoleService.API.Authentication
{
    public static class MeritJwtCookie
    {
        public const string Name = "Finance.Jwt";

        public static CookieOptions CreateOptions(DateTime? expiresUtc = null)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = expiresUtc
            };
        }

        public static void Clear(HttpResponse response)
        {
            response.Cookies.Delete(Name, CreateOptions());
        }
    }
}
