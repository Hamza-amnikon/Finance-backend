using PurchaseOrderService.API.DTOs;
using PurchaseOrderService.API.Models;

namespace PurchaseOrderService.API.Repositories;

public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> GetByIdAsync(int id, CancellationToken ct);
    Task<PurchaseOrder?> GetByRequestIdAsync(Guid requestId, CancellationToken ct);
    Task<(List<PurchaseOrder> Data, int Total)> GetPagedAsync(PurchaseOrderQueryDto query, CancellationToken ct);
    Task<PurchaseOrder> CreateAsync(PurchaseOrder order, CancellationToken ct);
    Task<PurchaseOrder?> UpdateDraftAsync(PurchaseOrder order, byte[] rowVersion, CancellationToken ct);
    Task<PurchaseOrder?> ChangeStatusAsync(int id, PurchaseOrderStatus status, byte[] rowVersion,
        Func<PurchaseOrder, CancellationToken, Task> validateIssue, CancellationToken ct);
}
