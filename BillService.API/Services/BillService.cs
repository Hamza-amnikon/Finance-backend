using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using BillService.API.Data;
using BillService.API.DTOs;
using BillService.API.Models;
using BillService.API.Repositories;

namespace BillService.API.Services
{
    public class BillService : IBillService
    {
        private readonly BillDbContext _context;
        private readonly IBillRepository _repository;
        private readonly IBillCalculator _calculator;
        private readonly IBillAccountingApiService _accountingApiService;
        private readonly IBillExchangeRateService _exchangeRateService;
        public BillService(
            BillDbContext db,
            IBillRepository repository,
            IBillCalculator calculator,
            IBillAccountingApiService accountingApiService,
            IBillExchangeRateService exchangeRateService
        )
        {
            _context = db;
            _repository = repository;
            _calculator = calculator;
            _accountingApiService = accountingApiService;
            _exchangeRateService = exchangeRateService;
        }

        public async Task<(List<BillResponseDto> Data, int Total)> GetPagedAsync(
            BillQueryDto query,
            CancellationToken cancellationToken
        )
        {
            Validator.ValidateObject(query, new(query), true);
            var(data, total) = await _repository.GetPagedAsync(query, cancellationToken);
            return (data.Select(ToResponse).ToList(), total);
        }

        public async Task<BillResponseDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken
        )
        {
            var bill = await _repository.GetByIdAsync(id, cancellationToken);
            return bill == null ? null : ToResponse(bill);
        }

        public async Task<BillResponseDto> PreviewAsync(
            BillFieldsDto dto,
            CancellationToken cancellationToken
        ) => ToResponse(await PrepareAsync(dto, cancellationToken));
        public async Task<BillResponseDto> CreateAsync(
            CreateBillDto dto,
            CancellationToken cancellationToken
        )
        {
            Validate(dto);
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto))));
            var existing = await _repository.GetByRequestIdAsync(dto.RequestId, cancellationToken);
            if (existing != null)
                return CheckRequest(existing, fingerprint);
            var bill = await PrepareAsync(dto, cancellationToken);
            bill.RequestId = dto.RequestId;
            bill.RequestFingerprint = fingerprint;
            try
            {
                return ToResponse(await _repository.CreateAsync(bill, cancellationToken));
            }
            catch (DbUpdateException error)when (error.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
            {
                _context.ChangeTracker.Clear();
                existing = await _repository.GetByRequestIdAsync(dto.RequestId, cancellationToken);
                if (existing == null)
                    throw;
                return CheckRequest(existing, fingerprint);
            }
        }

        private static BillResponseDto CheckRequest(
            Bill bill,
            string fingerprint
        )
        {
            if (bill.RequestFingerprint != fingerprint)
                throw new InvalidOperationException("This request ID was already used for different bill details.");
            return ToResponse(bill);
        }

        public async Task<BillResponseDto?> UpdateAsync(
            int id,
            UpdateBillDto dto,
            CancellationToken cancellationToken
        )
        {
            Validate(dto);
            var bill = await PrepareAsync(dto, cancellationToken);
            bill.Id = id;
            var saved = await _repository.UpdateDraftAsync(bill, dto.RowVersion, cancellationToken);
            return saved == null ? null : ToResponse(saved);
        }

        public async Task<BillResponseDto?> ChangeStatusAsync(
            int id,
            BillStatus status,
            byte[] rowVersion,
            CancellationToken cancellationToken
        )
        {
            if (rowVersion == null || rowVersion.Length != 8)
                throw new ArgumentException("A valid row version is required.");
            var saved = await _repository.ChangeStatusAsync(id, status, rowVersion, ValidateIssueAsync, cancellationToken);
            if (saved != null && status == BillStatus.Issued)
                await PostToAccountingAsync(saved, cancellationToken);
            return saved == null ? null : ToResponse(saved);
        }

        private async Task ValidateIssueAsync(
            Bill bill,
            CancellationToken cancellationToken
        )
        {
            if (string.IsNullOrWhiteSpace(bill.VendorInvoiceNumber))
                throw new ArgumentException("Enter the vendor invoice number before issuing the bill.");
            if (!bill.DueDate.HasValue)
                throw new ArgumentException("Enter a due date before issuing the bill.");
            if (bill.SourcePurchaseOrderId.HasValue)
            {
                var sourceId = bill.SourcePurchaseOrderId.Value;
                var status = await _context.Database.SqlQuery<string>($"SELECT [Status] AS [Value] FROM [purchases].[PurchaseOrders] WITH (HOLDLOCK) WHERE [Id] = {sourceId}").SingleOrDefaultAsync(cancellationToken);
                if (status != "Issued")
                    throw new InvalidOperationException("The source purchase order is no longer issued.");
            }

            if (!await _context.Vendors.AnyAsync(row => row.Id == bill.VendorId && row.IsActive, cancellationToken))
                throw new InvalidOperationException("The vendor is inactive or unavailable.");
            if (!await _context.Branches.AnyAsync(row => row.Name == bill.Branch && row.IsActive, cancellationToken))
                throw new InvalidOperationException("The managing branch is inactive or unavailable.");
            var branch = await _context.Branches.AsNoTracking().SingleAsync(row => row.Name == bill.Branch && row.IsActive, cancellationToken);
            if (!branch.AccountingCompanyId.HasValue)
                throw new InvalidOperationException("Link the bill branch to an accounting company before issuing.");
            bill.TaxTreatment = ResolveTaxTreatment(bill.TaxTreatment, branch.CountryCode, bill.TaxTotal);
            if (bill.Items.Any(item => string.IsNullOrWhiteSpace(item.AccountCode)))
                throw new ArgumentException("Select an expense or asset account for every bill line before issuing.");
            var currency = await _context.Currencies.SingleOrDefaultAsync(row => row.CurrencyCode == bill.Currency && row.IsActive, cancellationToken);
            if (currency == null || currency.DecimalPlaces != bill.CurrencyDecimalPlaces)
                throw new InvalidOperationException("Currency settings changed. Edit and save the draft before issuing.");
        }

        private async Task PostToAccountingAsync(
            Bill bill,
            CancellationToken cancellationToken
        )
        {
            var branch = await _context.Branches.AsNoTracking().SingleOrDefaultAsync(row => row.Name == bill.Branch && row.IsActive, cancellationToken) ?? throw new InvalidOperationException("The managing branch is inactive or unavailable.");
            if (!branch.AccountingCompanyId.HasValue)
                throw new InvalidOperationException("Link the bill branch to an accounting company before issuing.");
            var company = await _accountingApiService.GetCompanyAsync(branch.AccountingCompanyId.Value, cancellationToken) ?? throw new InvalidOperationException("The linked accounting company does not exist.");
            var billNumber = Clean(bill.BillNumber);
            if (billNumber.Length == 0)
                throw new InvalidOperationException("The issued bill number is missing.");
            var exchangeRate = await _exchangeRateService.GetRateAsync(bill.Currency, company.Currency, bill.BillDate, cancellationToken);
            var treatment = ResolveTaxTreatment(bill.TaxTreatment, branch.CountryCode, bill.TaxTotal);
            var posting = new BillAccountingPostingDto
            {
                CompanyId = company.Id,
                BillId = bill.Id,
                BillNumber = billNumber,
                BillDate = bill.BillDate,
                Branch = bill.Branch,
                Currency = bill.Currency,
                ExchangeRate = exchangeRate,
                VendorId = bill.VendorId,
                VendorName = bill.VendorName,
                NetAmount = bill.SubTotal - bill.DiscountTotal,
                TotalAmount = bill.GrandTotal,
                Expenses = bill.Items.Select(item => new BillAccountingExpenseDto { AccountCode = item.AccountCode, Description = string.IsNullOrWhiteSpace(item.Description) ? item.ItemName : item.Description, Amount = item.LineSubTotal - item.DiscountAmount }).ToList(),
                Taxes = BuildTaxPostings(treatment, bill.TaxTotal)
            };
            await _accountingApiService.PostBillAsync(posting, cancellationToken);
        }

        private static BillTaxTreatment ResolveTaxTreatment(
            BillTaxTreatment? selected,
            string countryCode,
            decimal taxTotal
        )
        {
            if (taxTotal == 0)
                return BillTaxTreatment.NoTax;
            var country = countryCode.Trim().ToUpperInvariant();
            if (country == "AE")
            {
                if (selected.HasValue && selected != BillTaxTreatment.UaeVat)
                    throw new ArgumentException("A taxable UAE bill must use UAE VAT tax treatment.");
                return BillTaxTreatment.UaeVat;
            }

            if (country == "IN")
            {
                if (selected != BillTaxTreatment.IndiaCgstSgst && selected != BillTaxTreatment.IndiaIgst)
                    throw new ArgumentException("Select India CGST + SGST or India IGST before issuing the bill.");
                return selected.Value;
            }

            throw new ArgumentException("Tax treatment is not configured for the branch country.");
        }

        private static List<BillAccountingTaxDto> BuildTaxPostings(
            BillTaxTreatment treatment,
            decimal taxTotal
        )
        {
            if (treatment == BillTaxTreatment.NoTax || taxTotal == 0)
                return new();
            if (treatment == BillTaxTreatment.UaeVat)
                return new()
                {
                    CreateTax("INPUT_VAT", "Input VAT on purchase bill", taxTotal)
                };
            if (treatment == BillTaxTreatment.IndiaIgst)
                return new()
                {
                    CreateTax("INPUT_IGST", "Input IGST on purchase bill", taxTotal)
                };
            var cgst = decimal.Round(taxTotal / 2m, 4, MidpointRounding.AwayFromZero);
            return new()
            {
                CreateTax("INPUT_CGST", "Input CGST on purchase bill", cgst),
                CreateTax("INPUT_SGST", "Input SGST on purchase bill", taxTotal - cgst)
            };
        }

        private static BillAccountingTaxDto CreateTax(
            string systemKey,
            string description,
            decimal amount
        ) => new()
        {
            SystemKey = systemKey,
            Description = description,
            Amount = amount
        };
        private async Task<Bill> PrepareAsync(
            BillFieldsDto dto,
            CancellationToken cancellationToken
        )
        {
            Validate(dto);
            var vendor = await _context.Vendors.SingleOrDefaultAsync(row => row.Id == dto.VendorId && row.IsActive, cancellationToken) ?? throw new ArgumentException("Select an active vendor.");
            var code = dto.Currency.Trim().ToUpperInvariant();
            var currency = await _context.Currencies.SingleOrDefaultAsync(row => row.CurrencyCode == code && row.IsActive, cancellationToken) ?? throw new ArgumentException("Select an active currency.");
            if (!await _context.Branches.AnyAsync(row => row.Name == dto.Branch.Trim() && row.IsActive, cancellationToken))
                throw new ArgumentException("Select an active managing branch.");
            if (currency.DecimalPlaces < 0 || currency.DecimalPlaces > 4)
                throw new ArgumentException("Invalid currency precision.");
            var address = string.Join(
                ", ",
                new[] { vendor.Address, vendor.City, vendor.State, vendor.Country, vendor.PostalCode }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim())
            );
            if (address.Length > 1000)
                throw new ArgumentException("Vendor address exceeds 1000 characters.");
            var bill = new Bill
            {
                VendorInvoiceNumber = Clean(dto.VendorInvoiceNumber),
                VendorId = vendor.Id,
                VendorName = vendor.Name,
                CompanyName = vendor.CompanyName,
                BillingAddress = dto.BillingAddress?.Trim() ?? address,
                ShippingAddress = Clean(dto.ShippingAddress),
                BillDate = dto.BillDate,
                DueDate = dto.DueDate,
                Reference = Clean(dto.Reference),
                PaymentTerms = Clean(dto.PaymentTerms),
                Notes = Clean(dto.Notes),
                AdditionalTerms = Clean(dto.AdditionalTerms),
                Branch = dto.Branch.Trim(),
                Currency = code,
                TaxTreatment = dto.TaxTreatment,
                CurrencyDecimalPlaces = currency.DecimalPlaces,
                Items = dto.Items.Select(item => new BillItem { ItemName = item.ItemName.Trim(), AccountCode = item.AccountCode.Trim(), Description = Clean(item.Description), Unit = item.Unit, Quantity = item.Quantity, UnitPrice = item.UnitPrice, DiscountEnabled = item.DiscountEnabled, DiscountAmount = item.DiscountAmount, TaxRate = item.TaxRate }).ToList()
            };
            _calculator.Calculate(bill, currency.DecimalPlaces);
            return bill;
        }

        private static string Clean(string? value) => value?.Trim() ?? "";
        private static void Validate(BillFieldsDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);
            Validator.ValidateObject(dto, new(dto), true);
            foreach (var item in dto.Items)
            {
                if (item == null)
                    throw new ArgumentException("An bill line cannot be empty.");
                Validator.ValidateObject(item, new(item), true);
            }
        }

        private static BillResponseDto ToResponse(Bill bill) => new()
        {
            Id = bill.Id,
            RequestId = bill.RequestId,
            SourcePurchaseOrderId = bill.SourcePurchaseOrderId,
            SourcePurchaseOrderNumber = bill.SourcePurchaseOrderNumber,
            VendorInvoiceNumber = bill.VendorInvoiceNumber,
            VendorId = bill.VendorId,
            BillNumber = bill.BillNumber,
            VendorName = bill.VendorName,
            CompanyName = bill.CompanyName,
            BillingAddress = bill.BillingAddress,
            ShippingAddress = bill.ShippingAddress,
            BillDate = bill.BillDate,
            DueDate = bill.DueDate,
            Reference = bill.Reference,
            PaymentTerms = bill.PaymentTerms,
            Notes = bill.Notes,
            AdditionalTerms = bill.AdditionalTerms,
            Branch = bill.Branch,
            Currency = bill.Currency,
            CurrencyDecimalPlaces = bill.CurrencyDecimalPlaces,
            SubTotal = bill.SubTotal,
            DiscountTotal = bill.DiscountTotal,
            TaxTotal = bill.TaxTotal,
            TaxTreatment = bill.TaxTreatment,
            GrandTotal = bill.GrandTotal,
            Status = bill.Status,
            CreatedDate = bill.CreatedDate,
            ModifiedDate = bill.ModifiedDate,
            IssuedDate = bill.IssuedDate,
            CancelledDate = bill.CancelledDate,
            RowVersion = bill.RowVersion,
            Items = bill.Items.OrderBy(item => item.LineNumber).Select(item => new BillItemResponseDto { Id = item.Id, LineNumber = item.LineNumber, ItemName = item.ItemName, AccountCode = item.AccountCode, Description = item.Description, Unit = item.Unit, Quantity = item.Quantity, UnitPrice = item.UnitPrice, DiscountEnabled = item.DiscountEnabled, DiscountAmount = item.DiscountAmount, TaxRate = item.TaxRate, LineSubTotal = item.LineSubTotal, TaxAmount = item.TaxAmount, LineTotal = item.LineTotal }).ToList()
        };
    }
}
