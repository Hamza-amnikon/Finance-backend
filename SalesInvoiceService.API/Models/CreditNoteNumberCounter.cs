using System.ComponentModel.DataAnnotations;

namespace SalesInvoiceService.API.Models
{
    public class CreditNoteNumberCounter
    {
        // Year and month, for example 202610.
        [Key]
        [MaxLength(6)]
        public string PeriodKey { get; set; } = string.Empty;

        public int LastNumber { get; set; }
    }
}