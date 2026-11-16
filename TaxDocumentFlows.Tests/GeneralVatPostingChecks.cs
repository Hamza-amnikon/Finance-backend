using System.Collections;
using System.Reflection;
using BillService.API.Models;
using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Services;

internal static class GeneralVatPostingChecks
{
    public static void Run()
    {
        var invoiceService = typeof(SalesInvoiceService.API.Services.SalesInvoiceService);
        var billService = typeof(BillService.API.Services.BillService);

        CheckPosting(invoiceService, SalesInvoiceTaxTreatment.GeneralVat, "OUTPUT_VAT");
        CheckPosting(typeof(SalesCreditNoteService), SalesInvoiceTaxTreatment.GeneralVat, "OUTPUT_VAT");
        CheckPosting(billService, BillTaxTreatment.GeneralVat, "INPUT_VAT");

        foreach (var country in new[] { "OM", "ZZ" })
        {
            var invoiceTreatment = Invoke(invoiceService, "ResolveTaxTreatment",
                SalesInvoiceTaxTreatment.GeneralVat, country, 7.125m, true);
            Check(Equals(invoiceTreatment, SalesInvoiceTaxTreatment.GeneralVat),
                "Configured general VAT is reusable without a country switch: " + country);
        }

        ExpectRejected(invoiceService, SalesInvoiceTaxTreatment.GeneralVat, "OM", false);
        ExpectRejected(billService, BillTaxTreatment.GeneralVat, "OM", false);
        ExpectRejected(invoiceService, SalesInvoiceTaxTreatment.GeneralVat, "AE", true);
        ExpectRejected(billService, BillTaxTreatment.GeneralVat, "IN", true);
    }

    private static void CheckPosting(Type service, object treatment, string expectedKey)
    {
        var postings = ((IEnumerable)Invoke(service, "BuildTaxPostings", treatment, 7.125m))
            .Cast<object>()
            .ToArray();
        Check(postings.Length == 1, service.Name + " creates one VAT posting");
        var posting = postings.Single();
        Check(Equals(posting.GetType().GetProperty("SystemKey")!.GetValue(posting), expectedKey) &&
            Equals(posting.GetType().GetProperty("Amount")!.GetValue(posting), 7.125m),
            service.Name + " preserves the configured tax amount, including three decimals");
    }

    private static void ExpectRejected(Type service, object treatment, string country, bool configured)
    {
        try
        {
            Invoke(service, "ResolveTaxTreatment", treatment, country, 7.125m, configured);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is ArgumentException)
        {
            Console.WriteLine("PASS: " + service.Name + " rejects an invalid VAT source for " + country);
            return;
        }

        throw new InvalidOperationException("An invalid VAT source was accepted.");
    }

    private static object Invoke(Type service, string name, params object[] arguments)
    {
        return service.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, arguments)!;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }

        Console.WriteLine("PASS: " + message);
    }
}
