using System.ComponentModel.DataAnnotations;

namespace PaymentMadeService.API.Models
{
    public class PaymentMadeNumberCounter
    {
        [Key]
        [MaxLength(6)]
        public string PeriodKey { get; set; } = "";

        public int LastNumber { get; set; }
    }
}
