using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Services
{
    public interface ISalesCreditNoteService
    {
        Task<SalesCreditNoteResponseDto> PreviewAsync(
            CreateSalesCreditNoteDto dto,
            CancellationToken cancellationToken);

        Task<SalesCreditNoteResponseDto> CreateAsync(
            CreateSalesCreditNoteDto dto,
            CancellationToken cancellationToken);

        Task<SalesCreditNoteResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<(
            List<SalesCreditNoteResponseDto> Items,
            int Total)> GetPagedAsync(
                int page,
                int pageSize,
                string? search,
                SalesCreditNoteStatus? status,
                CancellationToken cancellationToken);

        Task<SalesCreditNoteResponseDto?> IssueAsync(
            int id,
            IssueSalesCreditNoteDto dto,
            CancellationToken cancellationToken);
    }
}