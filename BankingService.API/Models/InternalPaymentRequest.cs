using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BankingService.API.Models
{
    public class InternalPaymentRequest
    {
        public int Id { get; set; }
        public Guid RequestId { get; set; }
        public int RequestingCompanyId { get; set; }
        public int PayingCompanyId { get; set; }
        public int ReceivingBankAccountId { get; set; }
        public DateOnly RequestDate { get; set; }
        public DateOnly RequiredDate { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "";

        public int CurrencyDecimalPlaces { get; set; }
        [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
        public decimal Amount { get; set; }

        [NotMapped]
        [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
        public decimal ReceivedAmount { get; set; }

        [NotMapped]
        [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
        public decimal RemainingAmount => Math.Max(0m, Amount - ReceivedAmount);

        [NotMapped]
        public string FundingStatus => Status == InternalPaymentRequestStatus.Approved
            ? ReceivedAmount >= Amount ? "Paid" : ReceivedAmount > 0m ? "Partially Paid" : "Awaiting payment"
            : Status;

        [MaxLength(20)]
        public string Status { get; set; } = InternalPaymentRequestStatus.Draft;

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        [MaxLength(500)]
        public string DecisionNote { get; set; } = "";

        [MaxLength(200)]
        public string CreatedBy { get; set; } = "";

        [MaxLength(200)]
        public string? DecidedBy { get; set; }

        [MaxLength(200)]
        public string? SubmittedBy { get; set; }

        [MaxLength(500)]
        public string? SubmissionNote { get; set; }

        [MaxLength(200)]
        public string? CancelledBy { get; set; }

        [MaxLength(500)]
        public string? CancellationNote { get; set; }

        public DateTime? CancelledDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public DateTime? DecidedDate { get; set; }
        public List<InternalPaymentRequestLine> Lines { get; set; } = [];

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }

    public class InternalPaymentRequestLine
    {
        public int Id { get; set; }
        public int InternalPaymentRequestId { get; set; }

        [MaxLength(200)]
        public string Purpose { get; set; } = "";

        [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
        public decimal Amount { get; set; }
    }

    public static class InternalPaymentRequestStatus
    {
        public const string Draft = "Draft";
        public const string Submitted = "Submitted";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";
    }
}
