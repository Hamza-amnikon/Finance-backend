using SalesInvoiceService.API.Models;

namespace SalesInvoiceService.API.Services
{
    public class SalesInvoiceCalculator : ISalesInvoiceCalculator
    {
        private const decimal MaximumAmount =
            99999999999999.9999m;

        public void Calculate(
            SalesInvoice invoice,
            int currencyDecimalPlaces)
        {
            ArgumentNullException.ThrowIfNull(invoice);

            if (currencyDecimalPlaces < 0 || currencyDecimalPlaces > 4)
            {
                throw new ArgumentException(
                    "Currency decimal places must be between 0 and 4.");
            }

            if (invoice.Status != SalesInvoiceStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Only draft invoice totals can be recalculated.");
            }

            if (invoice.Items == null || invoice.Items.Count == 0)
            {
                throw new ArgumentException(
                    "An invoice must contain at least one service item.");
            }

            var calculations = new List<(
                SalesInvoiceItem Item,
                decimal SubTotal,
                decimal Discount,
                decimal Tax,
                decimal Total)>();

            foreach (var item in invoice.Items)
            {
                if (string.IsNullOrWhiteSpace(item.ServiceName))
                {
                    throw new ArgumentException(
                        "Service name is required.");
                }

                if (!Enum.IsDefined(typeof(ServiceUnit), item.Unit))
                {
                    throw new ArgumentException(
                        "Select a valid service unit.");
                }

                ValidateDecimal(item.Quantity, "Quantity");
                ValidateDecimal(item.UnitPrice, "Unit price");
                ValidateDecimal(item.DiscountAmount, "Discount");
                ValidateDecimal(item.TaxRate, "Tax rate");

                if (item.Quantity <= 0)
                {
                    throw new ArgumentException(
                        "Quantity must be greater than zero.");
                }

                if (item.Unit == ServiceUnit.FixedFee &&
                    item.Quantity != 1)
                {
                    throw new ArgumentException(
                        "A fixed-fee service must have quantity 1.");
                }

                if (item.TaxRate > 100)
                {
                    throw new ArgumentException(
                        "Tax rate cannot exceed 100%.");
                }

                var subTotal = Round(
                    item.Quantity * item.UnitPrice,
                    currencyDecimalPlaces);

                ValidateDecimal(subTotal, "Line subtotal");

                var discount = Round(
                    item.DiscountAmount,
                    currencyDecimalPlaces);

                if (discount > subTotal)
                {
                    throw new ArgumentException(
                        "Discount cannot exceed the line subtotal.");
                }

                var taxableAmount = subTotal - discount;

                var tax = Round(
                    taxableAmount * item.TaxRate / 100m,
                    currencyDecimalPlaces);

                var total = taxableAmount + tax;

                ValidateDecimal(total, "Line total");

                calculations.Add((
                    item,
                    subTotal,
                    discount,
                    tax,
                    total));
            }

            var invoiceSubTotal = calculations.Sum(x => x.SubTotal);
            var invoiceDiscount = calculations.Sum(x => x.Discount);
            var invoiceTax = calculations.Sum(x => x.Tax);
            var invoiceTotal = calculations.Sum(x => x.Total);

            ValidateDecimal(invoiceSubTotal, "Invoice subtotal");
            ValidateDecimal(invoiceDiscount, "Invoice discount");
            ValidateDecimal(invoiceTax, "Invoice tax");
            ValidateDecimal(invoiceTotal, "Invoice total");

            // Apply results only after every line passes validation.
            var lineNumber = 1;

            foreach (var calculation in calculations)
            {
                calculation.Item.LineNumber = lineNumber++;
                calculation.Item.LineSubTotal = calculation.SubTotal;
                calculation.Item.DiscountAmount = calculation.Discount;
                calculation.Item.TaxAmount = calculation.Tax;
                calculation.Item.LineTotal = calculation.Total;
            }

            invoice.SubTotal = invoiceSubTotal;
            invoice.DiscountTotal = invoiceDiscount;
            invoice.TaxTotal = invoiceTax;
            invoice.GrandTotal = invoiceTotal;
        }

        private static decimal Round(decimal amount, int decimalPlaces)
        {
            return decimal.Round(
                amount,
                decimalPlaces,
                MidpointRounding.AwayFromZero);
        }

        private static void ValidateDecimal(decimal value, string field)
        {
            if (value < 0 || value > MaximumAmount)
            {
                throw new ArgumentException(
                    $"{field} is outside the supported range.");
            }

            if (decimal.Round(value, 4) != value)
            {
                throw new ArgumentException(
                    $"{field} cannot have more than four decimal places.");
            }
        }
    }
}