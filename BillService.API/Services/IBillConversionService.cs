using BillService.API.DTOs;

namespace BillService.API.Services
{
    public interface IBillConversionService
    {
        Task<BillResponseDto?> ConvertAsync(int purchaseOrderId, byte[] rowVersion, CancellationToken cancellationToken);
    }
}
