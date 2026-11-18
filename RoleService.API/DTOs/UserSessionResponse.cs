namespace RoleService.API.DTOs
{
    public class UserSessionResponse
    {
        public Guid UserId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public int RoleId { get; set; }

        public string Role { get; set; } = string.Empty;
    }
}
