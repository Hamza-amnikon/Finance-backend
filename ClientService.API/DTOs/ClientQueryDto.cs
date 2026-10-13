using System.ComponentModel.DataAnnotations;

namespace ClientService.API.DTOs
{
    public class ClientQueryDto
    {
        [Range(1, 1000000)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 25;

        [MaxLength(200)]
        public string? Search { get; set; }
    }
}