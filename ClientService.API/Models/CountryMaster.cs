using System.ComponentModel.DataAnnotations;

namespace ClientService.API.Models
{
    public class CountryMaster
    {
        [Key]
        public int CountryId { get; set; }

        [Required, MaxLength(100)]
        public string CountryName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string State { get; set; } = string.Empty;

        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required, MaxLength(2)]
        public string LocationCode { get; set; } = string.Empty;

        [MaxLength(3)]
        public string CurrencyCode { get; set; } = string.Empty;

        [MaxLength(100)]
        public string TimeZone { get; set; } = string.Empty;
    }
}