using PaymentMadeService.API.DTOs;
using PaymentMadeService.API.Models;

namespace PaymentMadeService.API.Repositories
{
    public interface IPaymentMadeRepository
    {
        Task<(List<PaymentMade> Data, int Total)> GetPagedAsync(
            PaymentMadeQueryDto query,
            CancellationToken cancellationToken);

        Task<PaymentMade?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<PaymentMade?> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken);

        Task<PaymentMade> RecordAsync(
            PaymentMade payment,
            byte[] billRowVersion,
            CancellationToken cancellationToken);

        Task<PaymentMade?> VoidAsync(
            int id,
            VoidPaymentMadeDto dto,
            CancellationToken cancellationToken);
    }
}
