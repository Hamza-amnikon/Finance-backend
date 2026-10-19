using PurchaseOrderService.API.Models;

namespace PurchaseOrderService.API.Services
{
    public class PurchaseOrderCalculator : IPurchaseOrderCalculator
    {
        private const decimal MaximumAmount =
            99999999999999.9999m;

        public void Calculate(
            PurchaseOrder estimate,
            int currencyDecimalPlaces)
        {
            ArgumentNullException.ThrowIfNull(estimate);

            if (currencyDecimalPlaces < 0 || currencyDecimalPlaces > 4)
            {
                throw new ArgumentException(
                    "Currency decimal places must be between 0 and 4.");
            }

            if (estimate.Status != PurchaseOrderStatus.Draft)
            {
                throw new InvalidOperationException(
                    "Only draft purchase order totals can be recalculated.");
            }

            if (estimate.Items == null || estimate.Items.Count == 0)
            {
                throw new ArgumentException(
                    "An purchase order must contain at least one item item.");
            }

            var calculations = new List<(
                PurchaseOrderItem Item,
                decimal SubTotal,
                decimal Discount,
                decimal Tax,
                decimal Total)>();

            foreach (var item in estimate.Items)
            {
                if (item == null)
                {
                    throw new ArgumentException(
                        "A item item cannot be empty.");
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
                    ValidateDecimal(item.DiscountAmount, "Discount");
                }
                ValidateDecimal(item.TaxRate, "Tax rate");

                if (item.Quantity <= 0)
                {
                    throw new ArgumentException(
                        "Quantity must be greater than zero.");
                }

                if (item.Unit == PurchaseUnit.FixedFee &&
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
                    ? Round(item.DiscountAmount, currencyDecimalPlaces)
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

            var estimateSubTotal = calculations.Sum(x => x.SubTotal);
            var estimateDiscount = calculations.Sum(x => x.Discount);
            var estimateTax = calculations.Sum(x => x.Tax);
            var estimateTotal = calculations.Sum(x => x.Total);

            ValidateDecimal(estimateSubTotal, "Purchase order subtotal");
            ValidateDecimal(estimateDiscount, "Purchase order discount");
            ValidateDecimal(estimateTax, "Purchase order tax");
            ValidateDecimal(estimateTotal, "Purchase order total");

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

            estimate.SubTotal = estimateSubTotal;
            estimate.DiscountTotal = estimateDiscount;
            estimate.TaxTotal = estimateTax;
            estimate.GrandTotal = estimateTotal;
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