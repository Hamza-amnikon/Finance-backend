using ClientService.API.DTOs;
using ClientService.API.Models;
using ClientService.API.Repositories;

namespace ClientService.API.Services
{
    public class ClientService : IClientService
    {
        private readonly IClientRepository _repository;
        private readonly IBranchLookupService _branchLookupService;

        public ClientService(
            IClientRepository repository,
            IBranchLookupService branchLookupService)
        {
            _repository = repository;
            _branchLookupService = branchLookupService;
        }

        public async Task<(List<ClientResponseDto> Data, int Total)>
            GetAllAsync(
                ClientQueryDto query,
                CancellationToken cancellationToken)
        {
            var result = await _repository.GetAllAsync(
                query.Page,
                query.PageSize,
                query.Search,
                cancellationToken);

            return (
                result.Data.Select(ToResponse).ToList(),
                result.Total);
        }

        public async Task<ClientResponseDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            var client = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            return client == null ? null : ToResponse(client);
        }

        public async Task<ClientResponseDto> CreateAsync(
            CreateClientDto dto,
            CancellationToken cancellationToken)
        {
            var branch = await ResolveBranchAsync(
                dto.Branch,
                cancellationToken);

            var client = new Client();

            ApplyFields(client, dto);
            client.Branch = branch;

            client.CreatedDate = DateTime.UtcNow;

            var created = await _repository.CreateAsync(
                client,
                cancellationToken);

            return ToResponse(created);
        }

        public async Task<ClientResponseDto?> UpdateAsync(
            Guid id,
            UpdateClientDto dto,
            CancellationToken cancellationToken)
        {
            var client = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (client == null)
            {
                return null;
            }

            var branch = await ResolveBranchAsync(
                dto.Branch,
                cancellationToken);

            ApplyFields(client, dto);
            client.Branch = branch;

            client.ModifiedDate = DateTime.UtcNow;

            var updated = await _repository.UpdateAsync(
                client,
                dto.RowVersion,
                cancellationToken);

            return ToResponse(updated);
        }

        public async Task<bool> ArchiveAsync(
            Guid id,
            byte[] rowVersion,
            CancellationToken cancellationToken)
        {
            var client = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (client == null)
            {
                return false;
            }

            client.IsActive = false;
            client.ModifiedDate = DateTime.UtcNow;

            await _repository.UpdateAsync(
                client,
                rowVersion,
                cancellationToken);

            return true;
        }

        private async Task<string> ResolveBranchAsync(
            string name,
            CancellationToken cancellationToken)
        {
            var branch = await _branchLookupService.GetActiveNameAsync(
                name,
                cancellationToken);

            if (branch == null)
            {
                throw new ArgumentException(
                    "Select an active managing branch.");
            }

            return branch;
        }

        private static string Clean(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static void ApplyFields(
            Client client,
            ClientFieldsDto dto)
        {
            client.Name = Clean(dto.Name);
            client.CompanyName = Clean(dto.CompanyName);

            client.Email = string.IsNullOrWhiteSpace(dto.Email)
                ? null
                : dto.Email.Trim();

            client.Phone = Clean(dto.Phone);
            client.Address = Clean(dto.Address);
            client.City = Clean(dto.City);
            client.State = Clean(dto.State);
            client.Country = Clean(dto.Country);
            client.PostalCode = Clean(dto.PostalCode);
            client.GstNumber = Clean(dto.GstNumber);
            client.Pan = Clean(dto.Pan);
            client.BankName = Clean(dto.BankName);
            client.AccountNumber = Clean(dto.AccountNumber);
            client.IfscCode = Clean(dto.IfscCode);
            client.IsVatRegistered = dto.IsVatRegistered;
            client.VatTrn = Clean(dto.VatTrn);
            client.Ein = Clean(dto.Ein);
            client.Iban = Clean(dto.Iban).ToUpperInvariant();
            client.SwiftCode = Clean(dto.SwiftCode).ToUpperInvariant();
            client.RoutingNumber = Clean(dto.RoutingNumber);
            client.Notes = Clean(dto.Notes);
            client.Branch = Clean(dto.Branch);
            client.Currency = Clean(dto.Currency).ToUpperInvariant();
            client.IsActive = dto.IsActive;
        }

        private static ClientResponseDto ToResponse(Client client)
        {
            return new ClientResponseDto
            {
                Id = client.Id,
                Name = client.Name,
                CompanyName = client.CompanyName,
                Email = client.Email,
                Phone = client.Phone,
                Address = client.Address,
                City = client.City,
                State = client.State,
                Country = client.Country,
                PostalCode = client.PostalCode,
                GstNumber = client.GstNumber,
                Pan = client.Pan,
                BankName = client.BankName,
                AccountNumber = client.AccountNumber,
                IfscCode = client.IfscCode,
                IsVatRegistered = client.IsVatRegistered,
                VatTrn = client.VatTrn,
                Ein = client.Ein,
                Iban = client.Iban,
                SwiftCode = client.SwiftCode,
                RoutingNumber = client.RoutingNumber,
                Notes = client.Notes,
                Branch = client.Branch,
                Currency = client.Currency,
                IsActive = client.IsActive,
                CreatedDate = client.CreatedDate,
                ModifiedDate = client.ModifiedDate,
                RowVersion = client.RowVersion
            };
        }
    }
}