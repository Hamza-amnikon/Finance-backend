using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SalesEstimateService.API.DTOs;
using SalesEstimateService.API.Models;
using SalesEstimateService.API.Repositories;

namespace SalesEstimateService.API.Services
{
    public class SalesEstimateConversionService : ISalesEstimateConversionService
    {
        private readonly ISalesEstimateService _estimateService;
        private readonly ISalesEstimateRepository _repository;
        private readonly IInvoiceApiService _invoiceApi;

        public SalesEstimateConversionService(
            ISalesEstimateService estimateService,
            ISalesEstimateRepository repository,
            IInvoiceApiService invoiceApi)
        {
            _estimateService = estimateService;
            _repository = repository;
            _invoiceApi = invoiceApi;
        }

        public async Task<SalesEstimateConversionResponseDto?> ConvertAsync(
            int id,
            ConvertSalesEstimateDto dto,
            CancellationToken cancellationToken)
        {
            var errors = new List<ValidationResult>();
            if (!Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true))
                throw new ArgumentException(string.Join(" ", errors.Select(error => error.ErrorMessage)));

            var estimate = await _estimateService.GetByIdAsync(id, cancellationToken);
            if (estimate == null)
                return null;

            if (estimate.Status == SalesEstimateStatus.Converted)
            {
                var linked = estimate.ConvertedInvoiceId.HasValue
                    ? await _invoiceApi.GetByIdAsync(estimate.ConvertedInvoiceId.Value, cancellationToken)
                    : null;

                if (linked == null || linked.SourceEstimateId != id)
                    throw new InvalidOperationException("The linked invoice could not be found. Review the conversion link before continuing.");

                CheckDates(linked, dto);
                return new SalesEstimateConversionResponseDto { Estimate = estimate, Invoice = linked };
            }

            if (estimate.Status != SalesEstimateStatus.Accepted)
                throw new InvalidOperationException("Only accepted estimates can be converted to an invoice.");

            if (!estimate.RowVersion.SequenceEqual(dto.RowVersion))
                throw new DbUpdateConcurrencyException();

            // The invoice API persists a unique source-estimate ID before returning.
            // A retry recovers the same invoice if this call or the link save fails.
            var invoice = await _invoiceApi.CreateFromEstimateAsync(id, dto, cancellationToken);
            if (invoice.Id < 1 || invoice.SourceEstimateId != id ||
                invoice.SourceEstimateRowVersion == null ||
                !invoice.SourceEstimateRowVersion.SequenceEqual(dto.RowVersion))
                throw new HttpRequestException("Invoice service returned a mismatched conversion response.");

            CheckDates(invoice, dto);

            var converted = await _repository.CompleteConversionAsync(
                id,
                invoice.Id,
                dto.RowVersion,
                cancellationToken);

            if (converted == null)
                throw new InvalidOperationException("Invoice was created but the estimate link is missing. Retry the same request to recover the link.");

            estimate.Status = converted.Status;
            estimate.ConvertedInvoiceId = converted.ConvertedInvoiceId;
            estimate.ConvertedDate = converted.ConvertedDate;
            estimate.ModifiedDate = converted.ModifiedDate;
            estimate.RowVersion = converted.RowVersion;

            return new SalesEstimateConversionResponseDto { Estimate = estimate, Invoice = invoice };
        }

        private static void CheckDates(ConvertedInvoiceDto invoice, ConvertSalesEstimateDto dto)
        {
            if (invoice.SourceEstimateInvoiceDate != dto.InvoiceDate || invoice.SourceEstimateDueDate != dto.DueDate ||
                !string.Equals(invoice.PreferredPaymentMode, dto.PreferredPaymentMode?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This estimate already has an invoice with different conversion dates or payment mode. Open the existing invoice.");
        }
    }
}
