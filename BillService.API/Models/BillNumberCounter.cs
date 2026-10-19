using System.ComponentModel.DataAnnotations;

namespace BillService.API.Models
{
    public class BillNumberCounter
    {
        [MaxLength(6)] public string PeriodKey { get; set; } = "";
        public int LastNumber { get; set; }
    }
}
