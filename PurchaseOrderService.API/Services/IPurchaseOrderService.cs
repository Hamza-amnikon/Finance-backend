using PurchaseOrderService.API.DTOs;
using PurchaseOrderService.API.Models;

namespace PurchaseOrderService.API.Services;

public interface IPurchaseOrderService
{
    Task<(List<PurchaseOrderResponseDto> Data, int Total)> GetPagedAsync(PurchaseOrderQueryDto query, CancellationToken ct);
    Task<PurchaseOrderResponseDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<PurchaseOrderResponseDto> PreviewAsync(PurchaseOrderFieldsDto dto, CancellationToken ct);
    Task<PurchaseOrderResponseDto> CreateAsync(CreatePurchaseOrderDto dto, CancellationToken ct);
    Task<PurchaseOrderResponseDto?> UpdateAsync(int id, UpdatePurchaseOrderDto dto, CancellationToken ct);
    Task<PurchaseOrderResponseDto?> ChangeStatusAsync(int id, PurchaseOrderStatus status, byte[] rowVersion, CancellationToken ct);
}
