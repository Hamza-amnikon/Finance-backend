namespace BillService.API.DTOs
{
    public class BillAccountingCompanyDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
    }

    public class BillAccountingPostingDto
    {
        public int CompanyId { get; set; }
        public int BillId { get; set; }
        public string BillNumber { get; set; } = string.Empty;
        public DateOnly BillDate { get; set; }
        public string Branch { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; } = 1m;
        public Guid VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public decimal NetAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public List<BillAccountingExpenseDto> Expenses { get; set; } = new();
        public List<BillAccountingTaxDto> Taxes { get; set; } = new();
    }

    public class BillAccountingExpenseDto
    {
        public string AccountCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class BillAccountingTaxDto
    {
        public string SystemKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class BillAccountingResultDto
    {
        public int JournalEntryId { get; set; }
        public string EntryNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool AlreadyExisted { get; set; }
    }
}
