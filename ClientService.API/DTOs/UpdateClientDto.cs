using System.ComponentModel.DataAnnotations;

namespace ClientService.API.DTOs
{
    public class UpdateClientDto : ClientFieldsDto
    {
        [Required]
        [MinLength(8)]
        [MaxLength(8)]
        public byte[] RowVersion { get; set; } = [];
    }
}