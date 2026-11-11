namespace RoleService.API.Models
{
    public static class RoleStatus
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";
        public const string ValidationPattern = "^(" + Active + "|" + Inactive + ")$";
    }
}
