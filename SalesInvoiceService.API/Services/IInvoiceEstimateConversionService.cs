using SalesInvoiceService.API.DTOs;

namespace SalesInvoiceService.API.Services
{
    public interface IInvoiceEstimateConversionService
    {
        Task<SalesInvoiceResponseDto> CreateAsync(CreateInvoiceFromEstimateDto dto, CancellationToken cancellationToken);
    }
}
