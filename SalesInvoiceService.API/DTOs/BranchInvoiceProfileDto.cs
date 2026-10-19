namespace SalesInvoiceService.API.DTOs
{
    public class BranchInvoiceProfileDto : BranchInvoiceProfileFieldsDto
    {
        public int Id { get; set; }

        public string BranchName { get; set; } = string.Empty;

        public byte[] RowVersion { get; set; } = [];
    }
}
