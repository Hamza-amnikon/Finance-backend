using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Services
{
    public class SalesCreditNoteCalculator : ISalesCreditNoteCalculator
    {
        private const decimal MaximumAmount =
            99999999999999.9999m;

        public void Calculate(
            SalesCreditNote creditNote,
            SalesInvoice originalInvoice,
            IReadOnlyCollection<SalesCreditNoteItem> issuedCreditItems,
            int decimalPlaces)
        {
            ArgumentNullException.ThrowIfNull(creditNote);
            ArgumentNullException.ThrowIfNull(originalInvoice);
            ArgumentNullException.ThrowIfNull(issuedCreditItems);

            if (decimalPlaces < 0 || decimalPlaces > 4)
                throw new ArgumentException(
                    "Currency decimal places must be between 0 and 4.");

            if (creditNote.Status != SalesCreditNoteStatus.Draft)
                throw new InvalidOperationException(
                    "Only draft credit notes can be recalculated.");

            if (originalInvoice.Status != SalesInvoiceStatus.Issued)
                throw new InvalidOperationException(
                    "Credit notes require an issued invoice.");

            if (originalInvoice.Id <= 0 ||
                creditNote.SalesInvoiceId != originalInvoice.Id)
                throw new ArgumentException(
                    "The credit note must belong to the original invoice.");

            if (creditNote.Items == null ||
                creditNote.Items.Count == 0 ||
                creditNote.Items.Any(item => item == null))
                throw new ArgumentException(
                    "Select at least one invoice service to credit.");

            if (creditNote.Items
                .GroupBy(item => item.SalesInvoiceItemId)
                .Any(group => group.Count() > 1))
                throw new ArgumentException(
                    "Select each invoice service only once.");

            if (originalInvoice.Items == null ||
                originalInvoice.Items.Any(item => item == null) ||
                originalInvoice.Items
                    .GroupBy(item => item.Id)
                    .Any(group => group.Count() > 1))
                throw new InvalidOperationException(
                    "Original invoice items are invalid.");

            var sources = originalInvoice.Items
                .ToDictionary(item => item.Id);

            if (issuedCreditItems.Any(item =>
                item == null ||
                !sources.ContainsKey(item.SalesInvoiceItemId)))
                throw new InvalidOperationException(
                    "Previous credits must belong to this invoice.");

            var calculations = new List<(
                SalesCreditNoteItem Item,
                SalesInvoiceItem Source,
                decimal SubTotal,
                decimal Discount,
                decimal Tax,
                decimal Total)>();

            foreach (var item in creditNote.Items)
            {
                if (!sources.TryGetValue(
                    item.SalesInvoiceItemId,
                    out var source))
                    throw new ArgumentException(
                        "A selected service does not belong to this invoice.");

                ValidateAmount(item.Quantity, "Credit quantity");

                if ((!item.CreditAmount.HasValue && item.Quantity <= 0) ||
                    (item.CreditAmount.HasValue && item.Quantity != 0) ||
                    decimal.Round(item.Quantity, 4) != item.Quantity)
                    throw new ArgumentException(
                        "Credit quantity must be positive with at most four decimal places.");

                ValidateAmount(source.Quantity, "Original quantity");
                ValidateAmount(source.LineSubTotal, "Original subtotal");
                ValidateAmount(source.DiscountAmount, "Original discount");
                ValidateAmount(source.TaxAmount, "Original tax");
                ValidateAmount(source.LineTotal, "Original total");

                if (source.Quantity <= 0 ||
                    source.DiscountAmount > source.LineSubTotal ||
                    source.LineTotal !=
                        source.LineSubTotal -
                        source.DiscountAmount +
                        source.TaxAmount)
                    throw new InvalidOperationException(
                        "Original invoice amounts are inconsistent.");

                var previous = issuedCreditItems
                    .Where(saved =>
                        saved.SalesInvoiceItemId == source.Id)
                    .ToList();

                foreach (var saved in previous)
                {
                    ValidateAmount(
                        saved.Quantity,
                        "Previously credited quantity");

                    ValidateAmount(
                        saved.LineSubTotal,
                        "Previously credited subtotal");

                    ValidateAmount(
                        saved.DiscountAmount,
                        "Previously credited discount");

                    ValidateAmount(
                        saved.TaxAmount,
                        "Previously credited tax");

                    ValidateAmount(
                        saved.LineTotal,
                        "Previously credited total");

                    if ((!saved.CreditAmount.HasValue && saved.Quantity <= 0) ||
                        (saved.CreditAmount.HasValue && (saved.Quantity != 0 || saved.CreditAmount != saved.LineSubTotal - saved.DiscountAmount)) ||
                        saved.LineTotal !=
                            saved.LineSubTotal -
                            saved.DiscountAmount +
                            saved.TaxAmount)
                        throw new InvalidOperationException(
                            "Previous credit amounts are inconsistent.");
                }

                var usedQuantity = previous.Sum(x => x.Quantity);
                var usedSubTotal = previous.Sum(x => x.LineSubTotal);
                var usedDiscount = previous.Sum(x => x.DiscountAmount);
                var usedTax = previous.Sum(x => x.TaxAmount);
                var usedTotal = previous.Sum(x => x.LineTotal);

                if (usedQuantity > source.Quantity ||
                    usedSubTotal > source.LineSubTotal ||
                    usedDiscount > source.DiscountAmount ||
                    usedTax > source.TaxAmount ||
                    usedTotal > source.LineTotal)
                    throw new InvalidOperationException(
                        "Previous credits exceed the original invoice service.");

                if (item.Quantity > source.Quantity - usedQuantity)
                    throw new ArgumentException(
                        "Credit quantity exceeds the remaining invoice service quantity.");

                var combinedQuantity = usedQuantity + item.Quantity;
                var fraction = combinedQuantity / source.Quantity;

                // Allocate cumulatively so the final credit includes
                // any remaining rounding difference.
                decimal Allocate(decimal original, decimal used) =>
                    (combinedQuantity == source.Quantity
                        ? original
                        : decimal.Round(
                            original * fraction,
                            decimalPlaces,
                            MidpointRounding.AwayFromZero)) - used;

                var subTotal = Allocate(
                    source.LineSubTotal, usedSubTotal);

                var discount = Allocate(
                    source.DiscountAmount, usedDiscount);

                var tax = Allocate(
                    source.TaxAmount, usedTax);

                // Amount credits do not reduce service quantity. Allocate the original
                // discount and tax against the net amount, including prior issued credits.
                if (item.CreditAmount.HasValue || previous.Any(saved => saved.CreditAmount.HasValue))
                {
                    var originalNet = source.LineSubTotal - source.DiscountAmount;
                    var usedNet = usedSubTotal - usedDiscount;
                    var requestedNet = item.CreditAmount ?? decimal.Round(
                        originalNet * item.Quantity / source.Quantity,
                        decimalPlaces, MidpointRounding.AwayFromZero);
                    ValidateAmount(requestedNet, "Credit amount");
                    if (requestedNet <= 0 || decimal.Round(requestedNet, decimalPlaces) != requestedNet)
                        throw new ArgumentException("Credit amount must be positive and use the invoice currency precision.");
                    if (originalNet <= 0 || requestedNet > originalNet - usedNet)
                        throw new ArgumentException("Credit amount exceeds the remaining service amount before tax.");

                    var combinedNet = usedNet + requestedNet;
                    decimal AllocateNet(decimal original, decimal used) =>
                        combinedNet == originalNet ? original - used : Math.Clamp(
                            decimal.Round(original * combinedNet / originalNet,
                                decimalPlaces, MidpointRounding.AwayFromZero) - used,
                            0, original - used);
                    discount = AllocateNet(source.DiscountAmount, usedDiscount);
                    subTotal = requestedNet + discount;
                    tax = AllocateNet(source.TaxAmount, usedTax);
                }

                var total = subTotal - discount + tax;

                ValidateAmount(subTotal, "Credit subtotal");
                ValidateAmount(discount, "Credit discount");
                ValidateAmount(tax, "Credit tax");
                ValidateAmount(total, "Credit total");

                if (discount > subTotal ||
                    total > source.LineTotal - usedTotal)
                    throw new ArgumentException(
                        "Credit exceeds the remaining service amount.");

                calculations.Add((
                    item,
                    source,
                    subTotal,
                    discount,
                    tax,
                    total));
            }

            var subTotalSum = calculations.Sum(x => x.SubTotal);
            var discountSum = calculations.Sum(x => x.Discount);
            var taxSum = calculations.Sum(x => x.Tax);
            var totalSum = calculations.Sum(x => x.Total);

            ValidateAmount(subTotalSum, "Credit note subtotal");
            ValidateAmount(discountSum, "Credit note discount");
            ValidateAmount(taxSum, "Credit note tax");
            ValidateAmount(totalSum, "Credit note total");

            if (totalSum <= 0)
                throw new ArgumentException(
                    "Credit total must be greater than zero. Increase the credit amount or quantity if it rounds to zero.");

            // Apply results only after every line passes validation.
            var lineNumber = 1;

            foreach (var line in calculations)
            {
                line.Item.LineNumber = lineNumber++;
                line.Item.ServiceName = line.Source.ServiceName;
                line.Item.Description = line.Source.Description;
                line.Item.Unit = line.Source.Unit;
                line.Item.UnitPrice = line.Source.UnitPrice;
                line.Item.DiscountEnabled = line.Source.DiscountEnabled;
                line.Item.DiscountAmount = line.Discount;
                line.Item.TaxRate = line.Source.TaxRate;
                line.Item.LineSubTotal = line.SubTotal;
                line.Item.TaxAmount = line.Tax;
                line.Item.LineTotal = line.Total;
            }

            creditNote.Currency = originalInvoice.Currency;
            creditNote.SubTotal = subTotalSum;
            creditNote.DiscountTotal = discountSum;
            creditNote.TaxTotal = taxSum;
            creditNote.GrandTotal = totalSum;
        }

        private static void ValidateAmount(
            decimal amount,
            string field)
        {
            if (amount < 0 || amount > MaximumAmount)
                throw new ArgumentException(
                    $"{field} is outside the supported range.");
        }
    }
}