using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using VendorService.API.DTOs;
using VendorService.API.Models;
using VendorService.API.Repositories;

namespace VendorService.API.Services
{
    public class VendorService : IVendorService
    {
        private readonly IVendorRepository _repository;

        public VendorService(IVendorRepository repository)
        {
            _repository = repository;
        }

        public async Task<(List<VendorResponseDto> Vendors, int Total)>
            GetPagedAsync(
                int page,
                int pageSize,
                string? search,
                bool? isActive,
                CancellationToken cancellationToken)
        {
            var (vendors, total) = await _repository.GetPagedAsync(
                page,
                pageSize,
                search,
                isActive,
                cancellationToken);

            return (vendors.Select(ToResponse).ToList(), total);
        }

        public async Task<VendorResponseDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            var vendor = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            return vendor == null ? null : ToResponse(vendor);
        }

        public async Task<VendorResponseDto> CreateAsync(
            CreateVendorDto dto,
            CancellationToken cancellationToken)
        {
            ValidateDto(dto);

            var vendor = new Vendor
            {
                Id = Guid.NewGuid(),
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = null
            };

            ApplyFields(vendor, dto);

            var created = await _repository.CreateAsync(
                vendor,
                cancellationToken);

            return ToResponse(created);
        }

        public async Task<VendorResponseDto?> UpdateAsync(
            Guid id,
            UpdateVendorDto dto,
            CancellationToken cancellationToken)
        {
            ValidateDto(dto);
            ValidateRowVersion(dto.RowVersion);

            var vendor = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (vendor == null)
            {
                return null;
            }

            EnsureCurrentVersion(vendor, dto.RowVersion);

            ApplyFields(vendor, dto);

            vendor.RowVersion = dto.RowVersion.ToArray();
            vendor.ModifiedDate = DateTime.UtcNow;

            var updated = await _repository.UpdateAsync(
                vendor,
                cancellationToken);

            return ToResponse(updated);
        }

        public async Task<bool> ArchiveAsync(
            Guid id,
            byte[] rowVersion,
            CancellationToken cancellationToken)
        {
            ValidateRowVersion(rowVersion);

            var vendor = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (vendor == null)
            {
                return false;
            }

            EnsureCurrentVersion(vendor, rowVersion);

            if (!vendor.IsActive)
            {
                return true;
            }

            vendor.IsActive = false;
            vendor.ModifiedDate = DateTime.UtcNow;
            vendor.RowVersion = rowVersion.ToArray();

            await _repository.UpdateAsync(
                vendor,
                cancellationToken);

            return true;
        }

        private static void ValidateDto(CreateVendorDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            Validator.ValidateObject(
                dto,
                new ValidationContext(dto),
                validateAllProperties: true);

            var currency = dto.Currency.Trim();

            if (currency.Length != 3 ||
                currency.Any(character =>
                    !((character >= 'A' && character <= 'Z') ||
                      (character >= 'a' && character <= 'z'))))
            {
                throw new ValidationException(
                    "Currency must be a three-letter code, such as INR, AED or USD.");
            }
        }

        private static void ValidateRowVersion(byte[]? rowVersion)
        {
            if (rowVersion == null || rowVersion.Length != 8)
            {
                throw new ValidationException(
                    "A valid rowVersion is required. Refresh the vendor and try again.");
            }
        }

        private static void EnsureCurrentVersion(
            Vendor vendor,
            byte[] rowVersion)
        {
            if (!vendor.RowVersion.SequenceEqual(rowVersion))
            {
                throw new DbUpdateConcurrencyException(
                    "This vendor has changed. Refresh the vendor and try again.");
            }
        }

        private static void ApplyFields(
            Vendor vendor,
            CreateVendorDto dto)
        {
            vendor.Name = dto.Name.Trim();
            vendor.CompanyName = Clean(dto.CompanyName);
            vendor.ContactPerson = Clean(dto.ContactPerson);

            // Email is nullable in the existing database.
            vendor.Email = string.IsNullOrWhiteSpace(dto.Email)
                ? null
                : dto.Email.Trim();

            vendor.Phone = Clean(dto.Phone);

            vendor.Address = Clean(dto.Address);
            vendor.City = Clean(dto.City);
            vendor.State = Clean(dto.State);
            vendor.Country = Clean(dto.Country);
            vendor.PostalCode = Clean(dto.PostalCode);

            vendor.GstNumber = CleanUpper(dto.GstNumber);
            vendor.Pan = CleanUpper(dto.Pan);

            vendor.IsVatRegistered = dto.IsVatRegistered;
            vendor.VatTrn = Clean(dto.VatTrn);

            vendor.Ein = Clean(dto.Ein);

            vendor.BankName = Clean(dto.BankName);
            vendor.AccountNumber = Clean(dto.AccountNumber);
            vendor.IfscCode = CleanUpper(dto.IfscCode);
            vendor.Iban = CleanUpper(dto.Iban);
            vendor.SwiftCode = CleanUpper(dto.SwiftCode);
            vendor.RoutingNumber = Clean(dto.RoutingNumber);

            vendor.PaymentTerms = Clean(dto.PaymentTerms);
            vendor.Notes = Clean(dto.Notes);

            vendor.Branch = dto.Branch.Trim();
            vendor.Currency = dto.Currency.Trim().ToUpperInvariant();
        }

        // Other text columns require non-null values.
        private static string Clean(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static string CleanUpper(string? value)
        {
            return Clean(value).ToUpperInvariant();
        }

        private static VendorResponseDto ToResponse(Vendor vendor)
        {
            return new VendorResponseDto
            {
                Id = vendor.Id,
                Name = vendor.Name,
                CompanyName = vendor.CompanyName,
                ContactPerson = vendor.ContactPerson,
                Email = vendor.Email,
                Phone = vendor.Phone,

                Address = vendor.Address,
                City = vendor.City,
                State = vendor.State,
                Country = vendor.Country,
                PostalCode = vendor.PostalCode,

                GstNumber = vendor.GstNumber,
                Pan = vendor.Pan,
                IsVatRegistered = vendor.IsVatRegistered,
                VatTrn = vendor.VatTrn,
                Ein = vendor.Ein,

                BankName = vendor.BankName,
                AccountNumber = vendor.AccountNumber,
                IfscCode = vendor.IfscCode,
                Iban = vendor.Iban,
                SwiftCode = vendor.SwiftCode,
                RoutingNumber = vendor.RoutingNumber,

                PaymentTerms = vendor.PaymentTerms,
                Notes = vendor.Notes,

                Branch = vendor.Branch,
                Currency = vendor.Currency,
                IsActive = vendor.IsActive,

                CreatedDate = vendor.CreatedDate,
                ModifiedDate = vendor.ModifiedDate,
                RowVersion = vendor.RowVersion.ToArray()
            };
        }
    }
}