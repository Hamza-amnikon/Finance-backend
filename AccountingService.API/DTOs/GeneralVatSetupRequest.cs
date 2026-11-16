using System.ComponentModel.DataAnnotations;

namespace AccountingService.API.DTOs
{
    public class GeneralVatSetupRequest
    {
        [Range(1, int.MaxValue)]
        public int CompanyId { get; set; }
    }
}
