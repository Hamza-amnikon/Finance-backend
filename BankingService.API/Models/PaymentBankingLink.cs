using System.ComponentModel.DataAnnotations;

namespace BankingService.API.Models
{
    public class PaymentBankingLink
    {
        public int Id { get; set; }

        [MaxLength(20)]
        public string Source { get; set; } = "";

        public int SourcePaymentId { get; set; }
        public Guid SourceRequestId { get; set; }

        [MaxLength(50)]
        public string PaymentNumber { get; set; } = "";

        [MaxLength(20)]
        public string Branch { get; set; } = "";

        [MaxLength(3)]
        public string Currency { get; set; } = "";

        public decimal Amount { get; set; }
        public int? BankTransactionId { get; set; }
        public int? ReversalTransactionId { get; set; }

        [MaxLength(20)]
        public string State { get; set; } = "Pending";

        [MaxLength(1000)]
        public string Message { get; set; } = "";

        public DateTime UpdatedAtUtc { get; set; }
        public DateTime NextAttemptAtUtc { get; set; }
    }

    public class PaymentBankingSettings
    {
        public int Id { get; set; }
        public DateTime SourceAfterUtc { get; set; }
    }

    // Materialized read-only from the original payment services' existing tables.
    public class PaymentBankingSource
    {
        public string Source { get; set; } = "";
        public int Id { get; set; }
        public Guid RequestId { get; set; }
        public string PaymentNumber { get; set; } = "";
        public string PartyName { get; set; } = "";
        public string Branch { get; set; } = "";
        public string Currency { get; set; } = "";
        public decimal Amount { get; set; }
        public string PaymentMode { get; set; } = "";
        public string Reference { get; set; } = "";
        public DateOnly PaymentDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? VoidedDate { get; set; }
        public string Status { get; set; } = "";
    }
}
