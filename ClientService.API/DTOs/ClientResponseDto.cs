namespace ClientService.API.DTOs
{
    public class ClientResponseDto : ClientFieldsDto
    {
        public Guid Id { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }
}