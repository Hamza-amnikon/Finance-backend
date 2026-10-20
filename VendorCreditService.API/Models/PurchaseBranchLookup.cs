namespace VendorCreditService.API.Models
{
    public class PurchaseBranchLookup
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        public bool IsActive { get; set; }
    }
}