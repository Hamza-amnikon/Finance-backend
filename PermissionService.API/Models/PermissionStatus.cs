namespace PermissionService.API.Models
{
    public static class PermissionStatus
    {
        public const string Active = "Active";

        public const string Inactive = "Inactive";

        public const string ValidationPattern = "^(" + Active + "|" + Inactive + ")$";
    }
}
