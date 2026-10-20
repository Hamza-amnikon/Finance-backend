namespace PaymentMadeService.API.Models
{
    public class PaymentCreditApplicationLookup
    {
        public int Id { get; set; }

        public int BillId { get; set; }

        public decimal Amount { get; set; }

        public string Status { get; set; } = "";
    }
}
