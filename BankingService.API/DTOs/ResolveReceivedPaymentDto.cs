using System.ComponentModel.DataAnnotations;

namespace BankingService.API.DTOs
{
    public class ResolveReceivedPaymentDto
    {
        [Range(1, int.MaxValue)]
        public int BankAccountId { get; set; }

        [Range(
            typeof(decimal),
            "0.0001",
            "99999999999999.9999")]
        public decimal BankAmount { get; set; }

        [Range(
            typeof(decimal),
            "0",
            "99999999999999.9999")]
        public decimal BankChargeAmount { get; set; }
    }
}
