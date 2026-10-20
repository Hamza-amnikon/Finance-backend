using VendorCreditService.API.Models;

namespace VendorCreditService.API.Services
{
    public class VendorCreditCalculator : IVendorCreditCalculator
    {
        private const decimal MaximumAmount =
            99999999999999.9999m;

        public void Calculate(
            VendorCredit credit,
            int currencyDecimalPlaces)
        {
            ArgumentNullException.ThrowIfNull(credit);

            if (
                currencyDecimalPlaces < 0 ||
                currencyDecimalPlaces > 4)
            {
                throw new ArgumentException(
                    "Currency decimal places must be between 0 and 4.");
            }

            if (credit.Status != VendorCreditStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Only draft vendor credit totals can be recalculated.");
            }

            if (credit.Items == null || credit.Items.Count == 0)
            {
                throw new ArgumentException(
                    "A vendor credit must contain at least one item.");
            }

            var calculations = new List<(
                VendorCreditItem Item,
                decimal SubTotal,
                decimal Discount,
                decimal Tax,
                decimal Total)>();

            foreach (var item in credit.Items)
            {
                if (item == null)
                {
                    throw new ArgumentException(
                        "A credit line cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(item.ItemName))
                {
                    throw new ArgumentException(
                        "Item name is required.");
                }

                if (!Enum.IsDefined(typeof(PurchaseUnit), item.Unit))
                {
                    throw new ArgumentException(
                        "Select a valid item unit.");
                }

                ValidateDecimal(item.Quantity, "Quantity");
                ValidateDecimal(item.UnitPrice, "Unit price");

                if (item.DiscountEnabled)
                {
                    ValidateDecimal(
                        item.DiscountAmount,
                        "Discount");
                }

                ValidateDecimal(item.TaxRate, "Tax rate");

                if (item.Quantity <= 0)
                {
                    throw new ArgumentException(
                        "Quantity must be greater than zero.");
                }

                if (
                    item.Unit == PurchaseUnit.FixedFee &&
                    item.Quantity != 1)
                {
                    throw new ArgumentException(
                        "A fixed-fee item must have quantity 1.");
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

                var discount = item.DiscountEnabled
                    ? Round(
                        item.DiscountAmount,
                        currencyDecimalPlaces)
                    : 0m;

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

            var creditSubTotal = calculations.Sum(
                row => row.SubTotal);

            var creditDiscount = calculations.Sum(
                row => row.Discount);

            var creditTax = calculations.Sum(
                row => row.Tax);

            var creditTotal = calculations.Sum(
                row => row.Total);

            ValidateDecimal(
                creditSubTotal,
                "Vendor credit subtotal");

            ValidateDecimal(
                creditDiscount,
                "Vendor credit discount");

            ValidateDecimal(
                creditTax,
                "Vendor credit tax");

            ValidateDecimal(
                creditTotal,
                "Vendor credit total");

            // Apply results after every line passes validation.
            var lineNumber = 1;

            foreach (var calculation in calculations)
            {
                calculation.Item.LineNumber = lineNumber++;
                calculation.Item.LineSubTotal = calculation.SubTotal;
                calculation.Item.DiscountAmount = calculation.Discount;
                calculation.Item.TaxAmount = calculation.Tax;
                calculation.Item.LineTotal = calculation.Total;
            }

            credit.SubTotal = creditSubTotal;
            credit.DiscountTotal = creditDiscount;
            credit.TaxTotal = creditTax;
            credit.GrandTotal = creditTotal;
        }

        private static decimal Round(
            decimal amount,
            int decimalPlaces)
        {
            return decimal.Round(
                amount,
                decimalPlaces,
                MidpointRounding.AwayFromZero);
        }

        private static void ValidateDecimal(
            decimal value,
            string field)
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