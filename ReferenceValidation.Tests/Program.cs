using System.ComponentModel.DataAnnotations;

var requests = new object[]
{
    new BankingService.API.DTOs.CreateBankTransactionDto(),
    new BankingService.API.DTOs.UpdateBankTransactionDto(),
    new BankingService.API.DTOs.CreateBankTransferDto(),
    new BankingService.API.DTOs.UpdateBankTransferDto(),
    new BankingService.API.DTOs.ConfirmBankTransferSentDto(),
    new BankingService.API.DTOs.CreateBankStatementLineDto(),
    new BillService.API.DTOs.BillFieldsDto(),
    new PurchaseOrderService.API.DTOs.PurchaseOrderFieldsDto(),
    new VendorCreditService.API.DTOs.VendorCreditFieldsDto(),
    new ExpenseService.API.DTOs.ExpenseDto(),
    new ExpenseService.API.DTOs.ExpenseClaimDto(),
    new PaymentMadeService.API.DTOs.CreatePaymentMadeDto(),
    new SalesInvoiceService.API.DTOs.CreateSalesInvoicePaymentDto(),
    new AccountingService.API.DTOs.CreateJournalEntryDto(),
    new AccountingService.API.DTOs.UpdateJournalEntryDto()
};

foreach (var request in requests)
{
    var property = request.GetType().GetProperty("Reference")!;
    var context = new ValidationContext(request) { MemberName = "Reference" };
    foreach (var value in new string?[] { null, "", " ", "\t\r\n" })
    {
        var errors = new List<ValidationResult>();
        if (Validator.TryValidateProperty(value, context, errors))
        {
            throw new Exception(request.GetType().FullName + " accepted a missing or whitespace-only reference.");
        }
    }
    foreach (var value in new[] { "REF-2026-001", "0", "PAY / 2026 / 01" })
    {
        if (!Validator.TryValidateProperty(value, context, []))
        {
            throw new Exception(request.GetType().FullName + " rejected a valid reference.");
        }
    }
    var maximum = property.GetCustomAttributes(typeof(MaxLengthAttribute), true)
        .Cast<MaxLengthAttribute>().Single().Length;
    if (Validator.TryValidateProperty(new string('A', maximum + 1), context, []))
    {
        throw new Exception(request.GetType().FullName + " lost its reference length limit.");
    }
    Console.WriteLine("PASS: " + request.GetType().FullName + " requires nonblank reference and retains its length limit.");
}
Console.WriteLine("Reference validation checks passed. No database records were changed.");
