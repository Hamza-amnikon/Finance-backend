using System.ComponentModel.DataAnnotations;

namespace PaymentMadeService.API.Models
{
    public class PaymentVendorLookup
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = "";

        public string CompanyName { get; set; } = "";

        public bool IsActive { get; set; }
    }
}
