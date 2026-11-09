namespace AccountingService.API.DTOs
{
    public class AgingReportRowDto
    {
        public int Id { get; set; }
        public string Kind { get; set; } = "";
        public string Number { get; set; } = "";
        public string PartyId { get; set; } = "";
        public string PartyName { get; set; } = "";
        public string Branch { get; set; } = "";
        public string Currency { get; set; } = "";
        public DateOnly Date { get; set; }
        public DateOnly? DueDate { get; set; }
        public decimal Total { get; set; }
        public decimal Paid { get; set; }
        public decimal Credits { get; set; }
        public decimal Balance { get; set; }
    }
}
