namespace RoleService.API.DTOs
{
    public class UserResponse
    {
        public Guid UserId { get; set; }

        public Guid TenantId { get; set; }

        public Guid ObjectId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public int RoleId { get; set; }

        public string Role { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }
}
