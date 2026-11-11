using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PurchaseOrderService.API.Data;
using PurchaseOrderService.API.DTOs;
using PurchaseOrderService.API.Models;

namespace PurchaseOrderService.API.Services
{
    public class PurchaseOrderTaxChoice
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Rate { get; set; }
        public string Treatment { get; set; } = "";
        public string ComponentsJson { get; set; } = "[]";
        public DateOnly EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    public class PurchaseOrderTaxComponent
    {
        public string Name { get; set; } = "";
        public decimal Rate { get; set; }
    }

    public class PurchaseOrderTaxSettingsService
    {
        private readonly PurchaseOrderDbContext _context;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public PurchaseOrderTaxSettingsService(PurchaseOrderDbContext context)
        {
            _context = context;
        }

        public async Task<List<PurchaseOrderTaxChoice>> ChoicesAsync(
            string branchName,
            DateOnly documentDate,
            CancellationToken cancellationToken)
        {
            if (documentDate == default)
            {
                throw new ArgumentException("Choose a document date before selecting tax.");
            }

            var branch = await _context.Branches.AsNoTracking()
                .SingleOrDefaultAsync(row => row.Name == branchName && row.IsActive, cancellationToken);

            if (branch?.AccountingCompanyId == null)
            {
                throw new ArgumentException("The selected branch must be linked to an accounting company.");
            }

            return await _context.Database.SqlQuery<PurchaseOrderTaxChoice>($"""
                SELECT r.Id, r.CompanyId, r.Code, r.Name, r.Rate, t.Treatment,
                    r.ComponentsJson, r.EffectiveFrom, r.EffectiveTo, r.RowVersion
                FROM accounting.TaxRates r
                INNER JOIN accounting.TaxTypes t ON t.Id = r.TaxTypeId
                INNER JOIN expenses.Companies c ON c.Id = r.CompanyId
                WHERE r.CompanyId = {branch.AccountingCompanyId.Value}
                    AND t.CountryCode = c.Country AND c.Country = {branch.CountryCode}
                    AND r.IsActive = 1 AND t.IsActive = 1
                    AND r.EffectiveFrom <= {documentDate}
                    AND (r.EffectiveTo IS NULL OR r.EffectiveTo >= {documentDate})
                ORDER BY r.Code, r.EffectiveFrom
                """).ToListAsync(cancellationToken);
        }

        private static PurchaseOrderTaxTreatment Treatment(PurchaseOrderTaxChoice choice)
        {
            if (!Enum.TryParse<PurchaseOrderTaxTreatment>(choice.Treatment, out var treatment) ||
                !Enum.IsDefined(treatment) || treatment == PurchaseOrderTaxTreatment.NoTax)
            {
                throw new ArgumentException("This tax setting uses an unsupported document treatment.");
            }

            var components = JsonSerializer.Deserialize<List<PurchaseOrderTaxComponent>>(choice.ComponentsJson, JsonOptions);
            if (components == null || components.Count == 0 || components.Any(row => row.Rate < 0 || row.Rate > 100) ||
                components.Sum(row => row.Rate) != choice.Rate || choice.Rate < 0 || choice.Rate > 100)
            {
                throw new ArgumentException("The selected tax setting has inconsistent components. Review Tax Settings.");
            }

            if (treatment == PurchaseOrderTaxTreatment.IndiaCgstSgst &&
                (components.Count != 2 || components[0].Rate != components[1].Rate))
            {
                throw new ArgumentException("The current CGST/SGST document posting requires two equal component rates. Review this tax setting.");
            }

            return treatment;
        }

        private static string Snapshot(PurchaseOrderTaxChoice choice)
        {
            return JsonSerializer.Serialize(choice, JsonOptions);
        }

        public async Task ApplyAsync(
            PurchaseOrder document,
            IReadOnlyList<PurchaseOrderItemRequestDto> requested,
            CancellationToken cancellationToken)
        {
            var choices = await ChoicesAsync(document.Branch, document.OrderDate, cancellationToken);
            PurchaseOrderTaxTreatment? treatment = null;

            for (var index = 0; index < requested.Count; index++)
            {
                var input = requested[index];
                var line = document.Items.ElementAt(index);
                if (!input.TaxSettingId.HasValue)
                {
                    line.TaxRate = 0;
                    line.TaxSettingId = null;
                    line.TaxSettingSnapshotJson = "";
                    continue;
                }

                var choice = choices.SingleOrDefault(row => row.Id == input.TaxSettingId.Value)
                    ?? throw new ArgumentException("The selected tax setting is inactive, outside the document date, or belongs to another company/country. Reload tax choices.");
                if (Convert.ToBase64String(choice.RowVersion) != input.TaxSettingRowVersion)
                {
                    throw new ArgumentException("The selected tax rate changed. Reload tax choices and select it again.");
                }

                var currentTreatment = Treatment(choice);
                if (treatment.HasValue && currentTreatment != treatment)
                {
                    throw new ArgumentException("All taxed lines on one document must use the same tax treatment. Do not mix CGST/SGST and IGST.");
                }

                treatment = currentTreatment;
                line.TaxRate = choice.Rate;
                line.TaxSettingId = choice.Id;
                line.TaxSettingSnapshotJson = Snapshot(choice);
            }

            document.TaxTreatment = treatment ?? PurchaseOrderTaxTreatment.NoTax;
            document.UsesTaxSettings = true;
        }

        public async Task ValidateDraftAsync(PurchaseOrder document, CancellationToken cancellationToken)
        {
            if (!document.UsesTaxSettings || document.Status != PurchaseOrderStatus.Draft)
            {
                return;
            }

            var choices = await ChoicesAsync(document.Branch, document.OrderDate, cancellationToken);
            PurchaseOrderTaxTreatment? treatment = null;
            foreach (var line in document.Items)
            {
                if (!line.TaxSettingId.HasValue)
                {
                    if (line.TaxRate != 0)
                    {
                        throw new ArgumentException("This draft has a tax percentage without a saved tax setting.");
                    }
                    continue;
                }

                var choice = choices.SingleOrDefault(row => row.Id == line.TaxSettingId.Value);
                if (choice == null || Snapshot(choice) != line.TaxSettingSnapshotJson || line.TaxRate != choice.Rate)
                {
                    throw new ArgumentException("A tax setting changed after this draft was created. Edit and save the draft with a currently valid setting before issuing.");
                }

                var selected = Treatment(choice);
                if (treatment.HasValue && selected != treatment)
                {
                    throw new ArgumentException("This draft mixes incompatible tax treatments.");
                }
                treatment = selected;
            }

            if (document.TaxTreatment != (treatment ?? PurchaseOrderTaxTreatment.NoTax))
            {
                throw new ArgumentException("The draft's tax treatment no longer matches its saved settings.");
            }
        }
    }
}
