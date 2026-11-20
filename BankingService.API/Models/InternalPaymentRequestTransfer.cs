using System.ComponentModel.DataAnnotations;

namespace BankingService.API.Models
{
    public sealed class InternalPaymentRequestTransfer
    {
        public int Id { get; set; }
        public int InternalPaymentRequestId { get; set; }
        public int BankTransferId { get; set; }
        public DateTime LinkedDate { get; set; }

        [MaxLength(200)]
        public string LinkedBy { get; set; } = "";
    }
}
