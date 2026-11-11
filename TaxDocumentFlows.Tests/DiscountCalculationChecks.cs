using I = SalesInvoiceService.API.Models;
using E = SalesEstimateService.API.Models;
using P = PurchaseOrderService.API.Models;
using B = BillService.API.Models;

internal static class DiscountCalculationChecks
{
    private sealed record Case(string Name, string Type, decimal? Value, decimal Recorded,
        decimal Quantity, decimal Price, bool Enabled, int Precision, decimal Discount, decimal Tax, decimal Total);
    private sealed record Result(decimal Discount, decimal Tax, decimal Total, decimal? Value, string Type);

    public static int Run()
    {
        var cases = new Case[]
        {
            new("Fixed 10", "Fixed", 10, 0, 1, 1000, true, 2, 10, 49.50m, 1039.50m),
            new("Percentage 10", "Percentage", 10, 0, 1, 1000, true, 2, 100, 45, 945),
            new("Percentage zero", "Percentage", 0, 0, 1, 1000, true, 2, 0, 50, 1050),
            new("Percentage 100", "Percentage", 100, 0, 1, 1000, true, 2, 1000, 0, 0),
            new("Fractional percentage", "Percentage", 12.5m, 0, 2, 80, true, 2, 20, 7, 147),
            new("Legacy amount", "Fixed", null, 10, 1, 1000, true, 2, 10, 49.50m, 1039.50m),
            new("Entered value overrides calculated amount", "Percentage", 10, 999, 1, 1000, true, 2, 100, 45, 945),
            new("Disabled discount", "Percentage", 10, 999, 1, 1000, false, 2, 0, 50, 1050),
            new("Changed quantity percentage", "Percentage", 10, 100, 2, 1000, true, 2, 200, 90, 1890),
            new("Changed quantity fixed", "Fixed", 10, 10, 2, 1000, true, 2, 10, 99.50m, 2089.50m),
            new("Two decimal currency", "Percentage", 33.3333m, 0, 1, 1, true, 2, 0.33m, 0.03m, 0.70m),
            new("Zero decimal currency", "Percentage", 10, 0, 1, 105, true, 0, 11, 5, 99),
            new("Four decimal currency", "Percentage", 33.3333m, 0, 1, 1, true, 4, 0.3333m, 0.0333m, 0.7000m)
        };
        var evaluators = new (string Name, Func<Case, Result> Calculate)[]
        {
            ("Invoice", Invoice), ("Estimate", Estimate), ("Purchase order", Order), ("Bill", Bill)
        };
        var checks = 0;
        foreach (var evaluator in evaluators)
        {
            foreach (var row in cases)
            {
                var result = evaluator.Calculate(row);
                if (result.Discount != row.Discount || result.Tax != row.Tax || result.Total != row.Total ||
                    result.Value != row.Value || result.Type != row.Type)
                {
                    throw new Exception($"FAIL: {evaluator.Name}: {row.Name}");
                }
                checks++;
            }
            var invalid = new Case[]
            {
                cases[1] with { Value = 101 },
                cases[1] with { Value = -1 },
                cases[1] with { Value = 0.00001m },
                cases[1] with { Value = null },
                cases[0] with { Value = 1001 },
                cases[0] with { Type = "Unknown" }
            };
            foreach (var row in invalid)
            {
                try
                {
                    evaluator.Calculate(row);
                }
                catch (ArgumentException)
                {
                    checks++;
                    continue;
                }
                throw new Exception($"FAIL: {evaluator.Name} accepted an invalid discount");
            }
            Console.WriteLine($"PASS: {evaluator.Name} fixed/percentage amounts, tax, rounding, repeated calculation and invalid inputs");
        }
        return checks;
    }

    private static void Configure(dynamic item, Case row)
    {
        item.Quantity = row.Quantity;
        item.UnitPrice = row.Price;
        item.DiscountEnabled = row.Enabled;
        item.DiscountType = row.Type;
        item.DiscountValue = row.Value;
        item.DiscountAmount = row.Recorded;
        item.TaxRate = 5m;
    }

    private static Result Read(dynamic document, dynamic item)
    {
        return new Result((decimal)item.DiscountAmount, (decimal)document.TaxTotal,
            (decimal)document.GrandTotal, (decimal?)item.DiscountValue, (string)item.DiscountType);
    }

    private static Result Invoice(Case row)
    {
        var item = new I.SalesInvoiceItem { ServiceName = "Test", Unit = I.ServiceUnit.Hour };
        Configure(item, row);
        var document = new I.SalesInvoice { Items = [item] };
        var calculator = new SalesInvoiceService.API.Services.SalesInvoiceCalculator();
        calculator.Calculate(document, row.Precision);
        calculator.Calculate(document, row.Precision);
        return Read(document, item);
    }

    private static Result Estimate(Case row)
    {
        var item = new E.SalesEstimateItem { ServiceName = "Test", Unit = E.ServiceUnit.Hour };
        Configure(item, row);
        var document = new E.SalesEstimate { Items = [item] };
        var calculator = new SalesEstimateService.API.Services.SalesEstimateCalculator();
        calculator.Calculate(document, row.Precision);
        calculator.Calculate(document, row.Precision);
        return Read(document, item);
    }

    private static Result Order(Case row)
    {
        var item = new P.PurchaseOrderItem { ItemName = "Test", Unit = P.PurchaseUnit.Piece };
        Configure(item, row);
        var document = new P.PurchaseOrder { Items = [item] };
        var calculator = new PurchaseOrderService.API.Services.PurchaseOrderCalculator();
        calculator.Calculate(document, row.Precision);
        calculator.Calculate(document, row.Precision);
        return Read(document, item);
    }

    private static Result Bill(Case row)
    {
        var item = new B.BillItem { ItemName = "Test", Unit = B.BillUnit.Piece };
        Configure(item, row);
        var document = new B.Bill { Items = [item] };
        var calculator = new BillService.API.Services.BillCalculator();
        calculator.Calculate(document, row.Precision);
        calculator.Calculate(document, row.Precision);
        return Read(document, item);
    }
}
