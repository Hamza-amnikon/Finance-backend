using SalesInvoiceService.API.DTOs;
using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Repositories;

namespace SalesInvoiceService.API.Services
{
    public class BranchService : IBranchService
    {
        private readonly IBranchRepository _repository;

        public BranchService(IBranchRepository repository)
        {
            _repository = repository;
        }

        public async Task<BranchInvoiceProfileDto?> GetInvoiceProfileAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var branch = await _repository.GetByIdAsync(id, cancellationToken);

            return branch == null ? null : ToInvoiceProfile(branch);
        }

        public async Task<BranchInvoiceProfileDto?> UpdateInvoiceProfileAsync(
            int id,
            UpdateBranchInvoiceProfileDto dto,
            CancellationToken cancellationToken)
        {
            var branch = await _repository.GetByIdAsync(id, cancellationToken);

            if (branch == null)
            {
                return null;
            }

            branch.CompanyName = dto.CompanyName?.Trim() ?? string.Empty;
            branch.Address = dto.Address?.Trim() ?? string.Empty;
            branch.CountryCode = dto.CountryCode?.Trim() ?? string.Empty;
            branch.CompanyRegistrationNumber = dto.CompanyRegistrationNumber?.Trim() ?? string.Empty;
            branch.TaxRegistrationNumber = dto.TaxRegistrationNumber?.Trim() ?? string.Empty;
            branch.BankAccountName = dto.BankAccountName?.Trim() ?? string.Empty;
            branch.BankName = dto.BankName?.Trim() ?? string.Empty;
            branch.BankAccountNumber = dto.BankAccountNumber?.Trim() ?? string.Empty;
            branch.BankIban = dto.BankIban?.Trim() ?? string.Empty;
            branch.BankSwiftCode = dto.BankSwiftCode?.Trim() ?? string.Empty;
            branch.Phone = dto.Phone?.Trim() ?? string.Empty;
            branch.Email = dto.Email?.Trim() ?? string.Empty;
            branch.InvoiceNotes = dto.InvoiceNotes?.Trim() ?? string.Empty;

            var updated = await _repository.UpdateInvoiceProfileAsync(
                branch,
                dto.RowVersion,
                cancellationToken);

            return ToInvoiceProfile(updated);
        }

        private static BranchInvoiceProfileDto ToInvoiceProfile(BranchMaster branch)
        {
            return new BranchInvoiceProfileDto
            {
                Id = branch.Id,
                BranchName = branch.Name,
                RowVersion = branch.RowVersion,
                CompanyName = branch.CompanyName,
                Address = branch.Address,
                CountryCode = branch.CountryCode,
                CompanyRegistrationNumber = branch.CompanyRegistrationNumber,
                TaxRegistrationNumber = branch.TaxRegistrationNumber,
                BankAccountName = branch.BankAccountName,
                BankName = branch.BankName,
                BankAccountNumber = branch.BankAccountNumber,
                BankIban = branch.BankIban,
                BankSwiftCode = branch.BankSwiftCode,
                Phone = branch.Phone,
                Email = branch.Email,
                InvoiceNotes = branch.InvoiceNotes,
            };
        }

        public Task<List<BranchMaster>> GetActiveAsync(
            CancellationToken cancellationToken)
        {
            return _repository.GetActiveAsync(cancellationToken);
        }

        public async Task<BranchMaster?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var value = name.Trim();

            if (value.Length > 20)
            {
                return null;
            }

            return await _repository.GetByNameAsync(
                value,
                cancellationToken);
        }
    }
}