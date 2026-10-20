using PaymentMadeService.API.DTOs;

namespace PaymentMadeService.API.Services
{
    public interface IPaymentMadeService
    {
        Task<(List<PaymentMadeResponseDto> Data, int Total)> GetPagedAsync(
            PaymentMadeQueryDto query,
            CancellationToken cancellationToken);

        Task<PaymentMadeResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<PaymentMadeResponseDto> CreateAsync(
            CreatePaymentMadeDto dto,
            CancellationToken cancellationToken);

        Task<PaymentMadeResponseDto?> VoidAsync(
            int id,
            VoidPaymentMadeDto dto,
            CancellationToken cancellationToken);

        Task<BillPaymentBalanceDto?> GetBillBalanceAsync(
            int billId,
            CancellationToken cancellationToken);
    }
}
