namespace AccountingService.API.Models
{
    public sealed class LedgerBranchAccessLookup
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
        public int? AccountingCompanyId { get; set; }
    }
}
