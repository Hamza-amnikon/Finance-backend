using System.ComponentModel.DataAnnotations;

namespace VendorCreditService.API.Models
{
    public class VendorCreditNumberCounter
    {
        [Key]
        [MaxLength(6)]
        public string PeriodKey { get; set; } = "";

        public int LastNumber { get; set; }
    }
}