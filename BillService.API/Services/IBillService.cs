using BillService.API.DTOs;
using BillService.API.Models;

namespace BillService.API.Services
{
    public interface IBillService
    {
        Task<(List<BillResponseDto> Data, int Total)> GetPagedAsync(BillQueryDto query, CancellationToken cancellationToken);
        Task<BillResponseDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<BillResponseDto> PreviewAsync(BillFieldsDto dto, CancellationToken cancellationToken);
        Task<BillResponseDto> CreateAsync(CreateBillDto dto, CancellationToken cancellationToken);
        Task<BillResponseDto?> UpdateAsync(int id, UpdateBillDto dto, CancellationToken cancellationToken);
        Task<BillResponseDto?> ChangeStatusAsync(int id, BillStatus status, byte[] rowVersion, CancellationToken cancellationToken);
    }
}
