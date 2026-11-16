namespace PermissionService.API.DTOs
{
    public class PermissionResponse
    {
        public int PermissionId { get; set; }

        public string PermissionCode { get; set; } = string.Empty;

        public string Module { get; set; } = string.Empty;

        public string PageName { get; set; } = string.Empty;

        public string Route { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }
}