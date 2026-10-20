using System.ComponentModel.DataAnnotations;

namespace VendorCreditService.API.Models
{
    public class CreditPaymentLookup
    {
        public int Id { get; set; }

        public int BillId { get; set; }

        public decimal Amount { get; set; }

        public string Status { get; set; } = "";
    }
}
