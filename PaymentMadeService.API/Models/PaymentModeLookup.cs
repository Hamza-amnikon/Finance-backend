using System.ComponentModel.DataAnnotations;

namespace PaymentMadeService.API.Models
{
    public class PaymentModeLookup
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        public bool IsActive { get; set; }
    }
}
