namespace AccountingService.API.Models
{
    public class AuditEvent
    {
        public long Id { get; set; }
        public DateTimeOffset AtUtc { get; set; }
        public string Service { get; set; } = "";
        public string EntityType { get; set; } = "";
        public string EntityId { get; set; } = "";
        public string Action { get; set; } = "";
        public string Actor { get; set; } = "";
        public string ActorKind { get; set; } = "";
        public string? Branch { get; set; }
        public int? CompanyId { get; set; }
        public string? CorrelationId { get; set; }
        public string? BeforeJson { get; set; }
        public string? AfterJson { get; set; }
    }
}
