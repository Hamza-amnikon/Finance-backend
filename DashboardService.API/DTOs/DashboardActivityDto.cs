namespace DashboardService.API.DTOs
{
    public class DashboardActivityDto
    {
        public string Id { get; set; } = "";

        public string EntityType { get; set; } = "";

        public string Action { get; set; } = "";

        public string Actor { get; set; } = "";

        public DateTime At { get; set; }
    }
}
