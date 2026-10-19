using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SalesEstimateService.API.DTOs;
using SalesEstimateService.API.Models;
using SalesEstimateService.API.Repositories;

namespace SalesEstimateService.API.Services
{
    public class SalesEstimateService : ISalesEstimateService
    {
        private readonly ISalesEstimateRepository _repository;
        private readonly IClientApiService _clientApiService;
        private readonly ICurrencyApiService _currencyApiService;
        private readonly ISalesEstimateCalculator _calculator;

        public SalesEstimateService(
            ISalesEstimateRepository repository,
            IClientApiService clientApiService,
            ICurrencyApiService currencyApiService,
            ISalesEstimateCalculator calculator)
        {
            _repository = repository;
            _clientApiService = clientApiService;
            _currencyApiService = currencyApiService;
            _calculator = calculator;
        }

        public async Task<SalesEstimateResponseDto> CreateAsync(
            CreateSalesEstimateDto dto,
            CancellationToken cancellationToken)
        {
            var estimate = await BuildDraftAsync(dto, cancellationToken);

            var created = await _repository.CreateAsync(
                estimate,
                cancellationToken);

            return ToResponse(created);
        }

        public async Task<SalesEstimateResponseDto> PreviewAsync(
            CreateSalesEstimateDto dto,
            CancellationToken cancellationToken)
        {
            var estimate = await BuildDraftAsync(dto, cancellationToken);

            return ToResponse(estimate);
        }

        public async Task<SalesEstimateResponseDto?> UpdateAsync(
            int id,
            UpdateSalesEstimateDto dto,
            CancellationToken cancellationToken)
        {
            Validate(dto);

            var existing = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            if (existing == null)
            {
                return null;
            }

            if (existing.Status != SalesEstimateStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Only draft estimates can be edited.");
            }

            if (!existing.RowVersion.SequenceEqual(dto.RowVersion))
            {
                throw new DbUpdateConcurrencyException();
            }

            var estimate = await BuildDraftAsync(dto, cancellationToken);
            estimate.Id = id;

            var updated = await _repository.UpdateDraftAsync(
                estimate,
                dto.RowVersion,
                cancellationToken);

            return updated == null ? null : ToResponse(updated);
        }

        public async Task<SalesEstimateResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken)
        {
            var estimate = await _repository.GetByIdAsync(
                id,
                cancellationToken);

            return estimate == null ? null : ToResponse(estimate);
        }

        public async Task<(List<SalesEstimateResponseDto> Data, int Total)>
            GetAllAsync(
                SalesEstimateQueryDto query,
                CancellationToken cancellationToken)
        {
            Validate(query);

            var result = await _repository.GetAllAsync(
                query.Page,
                query.PageSize,
                query.Search,
                query.Status,
                cancellationToken);

            return (result.Data.Select(ToResponse).ToList(), result.Total);
        }

        public Task<SalesEstimateResponseDto?> MarkAsSentAsync(
            int id,
            SalesEstimateActionDto dto,
            CancellationToken cancellationToken)
        {
            return ChangeStatusAsync(id, dto, SalesEstimateStatus.Sent, cancellationToken);
        }

        public Task<SalesEstimateResponseDto?> AcceptAsync(
            int id,
            SalesEstimateActionDto dto,
            CancellationToken cancellationToken)
        {
            return ChangeStatusAsync(id, dto, SalesEstimateStatus.Accepted, cancellationToken);
        }

        public Task<SalesEstimateResponseDto?> DeclineAsync(
            int id,
            SalesEstimateActionDto dto,
            CancellationToken cancellationToken)
        {
            return ChangeStatusAsync(id, dto, SalesEstimateStatus.Declined, cancellationToken);
        }

        private async Task<SalesEstimateResponseDto?> ChangeStatusAsync(
            int id,
            SalesEstimateActionDto dto,
            SalesEstimateStatus target,
            CancellationToken cancellationToken)
        {
            Validate(dto);

            var estimate = await _repository.GetByIdAsync(id, cancellationToken);

            if (estimate == null)
            {
                return null;
            }

            if (estimate.Status == target)
            {
                return ToResponse(estimate);
            }

            var expected = target == SalesEstimateStatus.Sent
                ? SalesEstimateStatus.Draft
                : SalesEstimateStatus.Sent;

            if (estimate.Status != expected)
            {
                throw new InvalidOperationException(
                    "This action is not allowed for the current estimate status.");
            }

            if (!estimate.RowVersion.SequenceEqual(dto.RowVersion))
            {
                throw new DbUpdateConcurrencyException();
            }

            if (target == SalesEstimateStatus.Sent)
            {
                // Recheck the client and totals without changing the saved draft.
                var calculated = await BuildDraftAsync(new CreateSalesEstimateDto
                {
                    ClientId = estimate.ClientId,
                    EstimateDate = estimate.EstimateDate,
                    ValidUntil = estimate.ValidUntil,
                    ServicePeriodStart = estimate.ServicePeriodStart,
                    ServicePeriodEnd = estimate.ServicePeriodEnd,
                    Currency = estimate.Currency,
                    Branch = estimate.Branch,
                    BillingAddress = estimate.BillingAddress,
                    Notes = estimate.Notes,
                    AdditionalTerms = estimate.AdditionalTerms,
                    Items = estimate.Items.OrderBy(item => item.LineNumber)
                        .Select(item => new SalesEstimateItemRequestDto
                        {
                            ServiceName = item.ServiceName,
                            Description = item.Description,
                            Unit = item.Unit,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            DiscountEnabled = item.DiscountEnabled,
                            DiscountAmount = item.DiscountAmount,
                            TaxRate = item.TaxRate
                        }).ToList()
                }, cancellationToken);

                var linesMatch = estimate.Items.OrderBy(item => item.LineNumber)
                    .Zip(calculated.Items, (saved, result) =>
                        saved.LineSubTotal == result.LineSubTotal &&
                        saved.DiscountAmount == result.DiscountAmount &&
                        saved.TaxAmount == result.TaxAmount &&
                        saved.LineTotal == result.LineTotal)
                    .All(matches => matches);

                if (!linesMatch || estimate.SubTotal != calculated.SubTotal ||
                    estimate.DiscountTotal != calculated.DiscountTotal ||
                    estimate.TaxTotal != calculated.TaxTotal ||
                    estimate.GrandTotal != calculated.GrandTotal)
                {
                    throw new InvalidOperationException(
                        "Draft totals no longer match the service lines. Review the draft before marking it as sent.");
                }
            }

            var changed = await _repository.ChangeStatusAsync(
                id,
                target,
                dto.RowVersion,
                cancellationToken);

            return changed == null ? null : ToResponse(changed);
        }

        private async Task<SalesEstimate> BuildDraftAsync(
            CreateSalesEstimateDto dto,
            CancellationToken cancellationToken)
        {
            Validate(dto);

            if (dto.Items.Any(item => item == null))
            {
                throw new ArgumentException("Add valid service lines.");
            }

            foreach (var item in dto.Items)
            {
                Validate(item);
            }

            var client = await _clientApiService.GetByIdAsync(
                dto.ClientId,
                cancellationToken);

            if (client == null || !client.IsActive)
            {
                throw new ArgumentException("Select an active client.");
            }

            if (client.Id != dto.ClientId)
            {
                throw new HttpRequestException(
                    "ClientService returned a different client.");
            }

            var branch = Clean(client.Branch);
            var code = Clean(client.Currency).ToUpperInvariant();

            if (branch.Length == 0 || branch.Length > 20)
            {
                throw new ArgumentException(
                    "Update the client's managing branch before creating an estimate.");
            }

            if (code.Length != 3 || code.Any(letter => letter < 'A' || letter > 'Z'))
            {
                throw new ArgumentException(
                    "Update the client's billing currency before creating an estimate.");
            }

            if (!string.Equals(Clean(dto.Branch), branch, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Clean(dto.Currency), code, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Branch and currency must match the selected client. Reload the client details.");
            }

            var currency = await _currencyApiService.GetByCodeAsync(
                code,
                cancellationToken);

            if (currency == null || !currency.IsActive)
            {
                throw new ArgumentException("The client's currency must be active.");
            }

            if (currency.CurrencyCode != code || currency.DecimalPlaces < 0 ||
                currency.DecimalPlaces > 4)
            {
                throw new HttpRequestException(
                    "SalesInvoiceService returned invalid currency details.");
            }

            var billingAddress = dto.BillingAddress == null
                ? string.Join(", ", new[]
                {
                    client.Address,
                    client.City,
                    client.State,
                    client.Country,
                    client.PostalCode
                }.Select(Clean).Where(value => value.Length > 0))
                : Clean(dto.BillingAddress);

            if (billingAddress.Length > 1000)
            {
                throw new ArgumentException(
                    "Billing address cannot exceed 1000 characters.");
            }

            var estimate = new SalesEstimate
            {
                ClientId = client.Id,
                ClientName = Clean(client.Name),
                CompanyName = Clean(client.CompanyName),
                BillingAddress = billingAddress,
                EstimateDate = dto.EstimateDate,
                ValidUntil = dto.ValidUntil,
                ServicePeriodStart = dto.ServicePeriodStart,
                ServicePeriodEnd = dto.ServicePeriodEnd,
                Branch = branch,
                Currency = code,
                Notes = Clean(dto.Notes),
                AdditionalTerms = Clean(dto.AdditionalTerms),
                Items = dto.Items.Select(item => new SalesEstimateItem
                {
                    ServiceName = Clean(item.ServiceName),
                    Description = Clean(item.Description),
                    Unit = item.Unit,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    DiscountEnabled = item.DiscountEnabled,
                    DiscountAmount = item.DiscountEnabled ? item.DiscountAmount : 0m,
                    TaxRate = item.TaxRate
                }).ToList()
            };

            _calculator.Calculate(estimate, currency.DecimalPlaces);

            return estimate;
        }

        private static void Validate(object value)
        {
            ArgumentNullException.ThrowIfNull(value);

            var errors = new List<ValidationResult>();

            if (!Validator.TryValidateObject(
                value,
                new ValidationContext(value),
                errors,
                validateAllProperties: true))
            {
                throw new ArgumentException(string.Join(
                    " ",
                    errors.Select(error => error.ErrorMessage)));
            }
        }

        private static string Clean(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static SalesEstimateResponseDto ToResponse(SalesEstimate estimate)
        {
            return new SalesEstimateResponseDto
            {
                Id = estimate.Id,
                ClientId = estimate.ClientId,
                EstimateNumber = estimate.EstimateNumber,
                ClientName = estimate.ClientName,
                CompanyName = estimate.CompanyName,
                BillingAddress = estimate.BillingAddress,
                EstimateDate = estimate.EstimateDate,
                ValidUntil = estimate.ValidUntil,
                ServicePeriodStart = estimate.ServicePeriodStart,
                ServicePeriodEnd = estimate.ServicePeriodEnd,
                Currency = estimate.Currency,
                Branch = estimate.Branch,
                SubTotal = estimate.SubTotal,
                DiscountTotal = estimate.DiscountTotal,
                TaxTotal = estimate.TaxTotal,
                GrandTotal = estimate.GrandTotal,
                Status = estimate.Status,
                Notes = estimate.Notes,
                AdditionalTerms = estimate.AdditionalTerms,
                CreatedDate = estimate.CreatedDate,
                ModifiedDate = estimate.ModifiedDate,
                SentDate = estimate.SentDate,
                AcceptedDate = estimate.AcceptedDate,
                DeclinedDate = estimate.DeclinedDate,
                CancelledDate = estimate.CancelledDate,
                ConvertedInvoiceId = estimate.ConvertedInvoiceId,
                ConvertedDate = estimate.ConvertedDate,
                RowVersion = estimate.RowVersion,
                Items = estimate.Items.OrderBy(item => item.LineNumber)
                    .Select(item => new SalesEstimateItemResponseDto
                    {
                        Id = item.Id,
                        LineNumber = item.LineNumber,
                        ServiceName = item.ServiceName,
                        Description = item.Description,
                        Unit = item.Unit,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        DiscountEnabled = item.DiscountEnabled,
                        DiscountAmount = item.DiscountAmount,
                        TaxRate = item.TaxRate,
                        LineSubTotal = item.LineSubTotal,
                        TaxAmount = item.TaxAmount,
                        LineTotal = item.LineTotal,
                    }).ToList()
            };
        }
    }
}
