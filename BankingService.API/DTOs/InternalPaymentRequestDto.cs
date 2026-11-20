using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BankingService.API.DTOs
{
    public class InternalPaymentRequestDto
    {
        public Guid RequestId { get; set; }

        [Range(1, int.MaxValue)]
        public int RequestingCompanyId { get; set; }

        [Range(1, int.MaxValue)]
        public int PayingCompanyId { get; set; }

        [Range(1, int.MaxValue)]
        public int ReceivingBankAccountId { get; set; }

        public DateOnly RequestDate { get; set; }
        public DateOnly RequiredDate { get; set; }

        [MaxLength(2000)]
        public string Notes { get; set; } = "";

        [Required]
        [MinLength(1)]
        [MaxLength(100)]
        public List<InternalPaymentRequestLineDto> Lines { get; set; } = [];

        public byte[]? RowVersion { get; set; }
    }

    public class InternalPaymentRequestLineDto
    {
        [Required]
        [MaxLength(200)]
        public string Purpose { get; set; } = "";

        [Range(typeof(decimal), "0.0001", "99999999999999.9999")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal Amount { get; set; }
    }

    public class InternalPaymentRequestTransferDto
    {
        [Range(1, int.MaxValue)]
        public int BankTransferId { get; set; }

        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];
    }

    public class InternalPaymentRequestActionDto
    {
        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];

        [MaxLength(500)]
        public string Note { get; set; } = "";
    }
}
