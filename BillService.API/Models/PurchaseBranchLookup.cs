namespace BillService.API.Models
{
    public class PurchaseBranchLookup
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string CountryCode { get; set; } = "";
        public int? AccountingCompanyId { get; set; }
        public bool IsActive { get; set; }
    }
}
