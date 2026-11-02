using System.ComponentModel.DataAnnotations;

namespace AccountingService.API.DTOs
{
    public class SetupChartOfAccountsDto
    {
        [Range(1, int.MaxValue)]
        public int CompanyId { get; set; }
    }
}