namespace SparkFinance.Discounts
{
    public static class LineDiscount
    {
        public static decimal Calculate(
            bool enabled,
            string type,
            decimal? value,
            decimal recordedAmount,
            decimal subtotal,
            int decimalPlaces)
        {
            if (type != "Fixed" && type != "Percentage")
            {
                throw new ArgumentException("Choose Fixed amount or Percentage for the discount type.");
            }
            if (!enabled)
            {
                return 0m;
            }
            if (type == "Percentage" && !value.HasValue)
            {
                throw new ArgumentException("Enter a discount percentage.");
            }
            var input = value ?? recordedAmount;
            if (input < 0 || input > 99999999999999.9999m || decimal.Round(input, 4) != input)
            {
                throw new ArgumentException("Enter a non-negative discount with at most four decimal places.");
            }
            if (type == "Percentage" && input > 100m)
            {
                throw new ArgumentException("Discount percentage must be between 0 and 100.");
            }
            var amount = type == "Percentage" ? subtotal * input / 100m : input;
            amount = decimal.Round(amount, decimalPlaces, MidpointRounding.AwayFromZero);
            if (amount > subtotal)
            {
                throw new ArgumentException("Discount cannot exceed the line subtotal.");
            }
            return amount;
        }
    }
}
