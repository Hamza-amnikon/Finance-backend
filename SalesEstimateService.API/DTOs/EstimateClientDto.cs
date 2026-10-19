namespace SalesEstimateService.API.DTOs
{
    public class EstimateClientDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public string State { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public string PostalCode { get; set; } = string.Empty;

        public string Branch { get; set; } = string.Empty;

        public string Currency { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}