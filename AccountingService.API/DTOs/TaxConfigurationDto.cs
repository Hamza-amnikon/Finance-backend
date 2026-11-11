using System.ComponentModel.DataAnnotations;

namespace AccountingService.API.DTOs
{
    public class TaxComponentDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = "";
        [Range(typeof(decimal), "0", "100")]
        public decimal Rate { get; set; }
    }

    public class TaxConfigurationDto
    {
        [Range(1, int.MaxValue)]
        public int CompanyId { get; set; }
        [Range(1, int.MaxValue)]
        public int TaxTypeId { get; set; }
        [Required, MaxLength(30), RegularExpression("^[A-Za-z0-9_-]+$")]
        public string Code { get; set; } = "";
        [Required, MaxLength(100)]
        public string Name { get; set; } = "";
        public DateOnly EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }
        public bool IsActive { get; set; } = true;
        [Required, MinLength(1)]
        public List<TaxComponentDto> Components { get; set; } = [];
        public string RowVersion { get; set; } = "";
    }
}
