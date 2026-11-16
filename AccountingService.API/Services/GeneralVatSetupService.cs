using System.ComponentModel.DataAnnotations;
using System.Data;
using AccountingService.API.Data;
using AccountingService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingService.API.Services
{
    public class GeneralVatSetupService
    {
        private readonly AccountingDbContext _context;
        private readonly IChartOfAccountsSetupService _accounts;

        public GeneralVatSetupService(
            AccountingDbContext context,
            IChartOfAccountsSetupService accounts)
        {
            _context = context;
            _accounts = accounts;
        }

        public async Task SetupAsync(
            int companyId,
            CancellationToken cancellationToken)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var company = await _context.Companies
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    row => row.Id == companyId,
                    cancellationToken);

            if (company == null ||
                string.IsNullOrWhiteSpace(company.Country))
            {
                throw new ValidationException(
                    "Select an existing company with a configured country.");
            }

            var countryCode = company.Country.Trim().ToUpperInvariant();
            var types = await _context.TaxTypes
                .Where(row => row.CountryCode == countryCode)
                .ToListAsync(cancellationToken);

            if (types.Any(type => type.Treatment != "GeneralVat"))
            {
                throw new ValidationException(
                    "This country already has a different tax configuration. " +
                    "Use its existing tax types.");
            }

            var vat = types.SingleOrDefault(type =>
                string.Equals(type.Code, "VAT", StringComparison.OrdinalIgnoreCase));

            if (vat == null)
            {
                vat = new TaxConfigurationType
                {
                    Code = "VAT",
                    Name = "VAT",
                    CountryCode = countryCode,
                    Treatment = "GeneralVat",
                    ComponentNamesJson = "[\"VAT\"]",
                    IsActive = true
                };

                _context.TaxTypes.Add(vat);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else if (!vat.IsActive)
            {
                throw new ValidationException(
                    "This country's VAT type is inactive. Review its configuration.");
            }

            await _accounts.SetupAsync(companyId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
