using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingService.API.Models
{
    [Table("TaxTypes", Schema = "accounting")]
    public class TaxConfigurationType
    {
        public int Id { get; set; }
        [MaxLength(30)]
        public string Code { get; set; } = "";
        [MaxLength(100)]
        public string Name { get; set; } = "";
        [MaxLength(2)]
        public string CountryCode { get; set; } = "";
        [MaxLength(40)]
        public string Treatment { get; set; } = "";
        public string ComponentNamesJson { get; set; } = "[]";
        public bool IsActive { get; set; }
    }

    [Table("TaxRates", Schema = "accounting")]
    public class TaxConfigurationRate
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int TaxTypeId { get; set; }
        [MaxLength(30)]
        public string Code { get; set; } = "";
        [MaxLength(100)]
        public string Name { get; set; } = "";
        [Column(TypeName = "decimal(9,4)")]
        public decimal Rate { get; set; }
        public DateOnly EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }
        public string ComponentsJson { get; set; } = "[]";
        public bool IsActive { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}
