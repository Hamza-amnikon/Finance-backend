namespace AccountingService.API.Models
{
    public sealed class LedgerPagePermissionLookup
    {
        public int PermissionId { get; set; }
        public string PermissionCode { get; set; } = "";
        public string Module { get; set; } = "";
        public string PageName { get; set; } = "";
        public string Route { get; set; } = "";
        public string Status { get; set; } = "";
    }
}
