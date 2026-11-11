using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BillService.API.Data;
using BillService.API.DTOs;
using BillService.API.Models;

namespace BillService.API.Services
{
    public class BillTaxChoice
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

    public class BillTaxComponent
    {
        public string Name { get; set; } = "";
        public decimal Rate { get; set; }
    }

    public class BillTaxSettingsService
    {
        private readonly BillDbContext _context;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public BillTaxSettingsService(BillDbContext context)
        {
            _context = context;
        }

        public async Task<List<BillTaxChoice>> ChoicesAsync(
            string branchName,
            DateOnly billDate,
            CancellationToken cancellationToken)
        {
            if (billDate == default)
            {
                throw new ArgumentException("Choose an bill date before selecting tax.");
            }

            var branch = await _context.Branches.AsNoTracking()
                .SingleOrDefaultAsync(row => row.Name == branchName && row.IsActive, cancellationToken);

            if (branch?.AccountingCompanyId == null)
            {
                throw new ArgumentException("The selected branch must be linked to an accounting company.");
            }

            return await _context.Database.SqlQuery<BillTaxChoice>($"""
                SELECT r.Id, r.CompanyId, r.Code, r.Name, r.Rate, t.Treatment,
                    r.ComponentsJson, r.EffectiveFrom, r.EffectiveTo, r.RowVersion
                FROM accounting.TaxRates r
                INNER JOIN accounting.TaxTypes t ON t.Id = r.TaxTypeId
                INNER JOIN expenses.Companies c ON c.Id = r.CompanyId
                WHERE r.CompanyId = {branch.AccountingCompanyId.Value}
                    AND t.CountryCode = c.Country AND c.Country = {branch.CountryCode}
                    AND r.IsActive = 1 AND t.IsActive = 1
                    AND r.EffectiveFrom <= {billDate}
                    AND (r.EffectiveTo IS NULL OR r.EffectiveTo >= {billDate})
                ORDER BY r.Code, r.EffectiveFrom
                """).ToListAsync(cancellationToken);
        }

        private static BillTaxTreatment Treatment(BillTaxChoice choice)
        {
            if (!Enum.TryParse<BillTaxTreatment>(choice.Treatment, out var treatment) ||
                !Enum.IsDefined(treatment) || treatment == BillTaxTreatment.NoTax)
            {
                throw new ArgumentException("This tax setting uses an unsupported bill treatment.");
            }

            var components = JsonSerializer.Deserialize<List<BillTaxComponent>>(choice.ComponentsJson, JsonOptions);
            if (components == null || components.Count == 0 || components.Any(row => row.Rate < 0 || row.Rate > 100) ||
                components.Sum(row => row.Rate) != choice.Rate || choice.Rate < 0 || choice.Rate > 100)
            {
                throw new ArgumentException("The selected tax setting has inconsistent components. Review Tax Settings.");
            }

            if (treatment == BillTaxTreatment.IndiaCgstSgst &&
                (components.Count != 2 || components[0].Rate != components[1].Rate))
            {
                throw new ArgumentException("The current CGST/SGST bill posting requires two equal component rates. Review this tax setting.");
            }

            return treatment;
        }

        private static string Snapshot(BillTaxChoice choice)
        {
            return JsonSerializer.Serialize(choice, JsonOptions);
        }

        public async Task ApplyAsync(
            Bill bill,
            IReadOnlyList<BillItemRequestDto> requested,
            CancellationToken cancellationToken)
        {
            var choices = await ChoicesAsync(bill.Branch, bill.BillDate, cancellationToken);
            BillTaxTreatment? treatment = null;

            for (var index = 0; index < requested.Count; index++)
            {
                var input = requested[index];
                var line = bill.Items.ElementAt(index);
                if (!input.TaxSettingId.HasValue)
                {
                    line.TaxRate = 0;
                    line.TaxSettingId = null;
                    line.TaxSettingSnapshotJson = "";
                    continue;
                }

                var choice = choices.SingleOrDefault(row => row.Id == input.TaxSettingId.Value)
                    ?? throw new ArgumentException("The selected tax setting is inactive, outside the bill date, or belongs to another company/country. Reload tax choices.");
                if (Convert.ToBase64String(choice.RowVersion) != input.TaxSettingRowVersion)
                {
                    throw new ArgumentException("The selected tax rate changed. Reload tax choices and select it again.");
                }

                var currentTreatment = Treatment(choice);
                if (treatment.HasValue && currentTreatment != treatment)
                {
                    throw new ArgumentException("All taxed lines on one bill must use the same tax treatment. Do not mix CGST/SGST and IGST.");
                }

                treatment = currentTreatment;
                line.TaxRate = choice.Rate;
                line.TaxSettingId = choice.Id;
                line.TaxSettingSnapshotJson = Snapshot(choice);
            }

            bill.TaxTreatment = treatment ?? BillTaxTreatment.NoTax;
            bill.UsesTaxSettings = true;
        }

        public async Task ValidateDraftAsync(Bill bill, CancellationToken cancellationToken)
        {
            if (!bill.UsesTaxSettings || bill.Status != BillStatus.Draft)
            {
                return;
            }

            var choices = await ChoicesAsync(bill.Branch, bill.BillDate, cancellationToken);
            BillTaxTreatment? treatment = null;
            foreach (var line in bill.Items)
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

            if (bill.TaxTreatment != (treatment ?? BillTaxTreatment.NoTax))
            {
                throw new ArgumentException("The draft's tax treatment no longer matches its saved settings.");
            }
        }
    }
}
