using VendorCreditService.API.DTOs;
using VendorCreditService.API.Models;

namespace VendorCreditService.API.Services
{
    public interface IVendorCreditService
    {
        Task<(
            List<VendorCreditResponseDto> Data,
            int Total)> GetPagedAsync(
                VendorCreditQueryDto query,
                CancellationToken cancellationToken);

        Task<VendorCreditResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken);

        Task<VendorCreditResponseDto> PreviewAsync(
            VendorCreditFieldsDto dto,
            CancellationToken cancellationToken);

        Task<VendorCreditResponseDto> CreateAsync(
            CreateVendorCreditDto dto,
            CancellationToken cancellationToken);

        Task<VendorCreditResponseDto?> UpdateAsync(
            int id,
            UpdateVendorCreditDto dto,
            CancellationToken cancellationToken);

        Task<VendorCreditResponseDto?> ChangeStatusAsync(
            int id,
            VendorCreditStatus status,
            byte[] rowVersion,
            CancellationToken cancellationToken);
    }
}