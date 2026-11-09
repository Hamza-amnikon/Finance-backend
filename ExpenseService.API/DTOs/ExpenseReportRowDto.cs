namespace ExpenseService.API.DTOs
{
    public class ExpenseReportRowDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = "";
        public string Number { get; set; } = "";
        public string PartyKey { get; set; } = "";
        public string PartyName { get; set; } = "";
        public string Branch { get; set; } = "";
        public string Category { get; set; } = "";
        public string Currency { get; set; } = "";
        public DateOnly Date { get; set; }
        public DateOnly? DueDate { get; set; }
        public decimal Net { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public decimal Balance { get; set; }
        public decimal Overdue { get; set; }
        public string Status { get; set; } = "";
    }
}
