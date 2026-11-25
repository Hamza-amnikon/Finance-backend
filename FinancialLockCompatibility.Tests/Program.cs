using Microsoft.EntityFrameworkCore;
using SparkFinance.Database;

const string testConnection = "Server=unused;Database=OfflineOnly;Integrated Security=True;TrustServerCertificate=True";
var checks = 0;
void CheckLock<TEntity>(DbContext context, string table, string column) where TEntity : class
{
    var sql = FinancialRowLock.BuildCommandText<TEntity>(context, column);
    var expected = $"SELECT * FROM {table} WITH (UPDLOCK,HOLDLOCK) WHERE [{column}] = {{0}}";
    if (sql != expected)
    {
        throw new Exception("Unexpected lock query for " + typeof(TEntity).FullName + ": " + sql);
    }
    checks++;
    Console.WriteLine("PASS existing lock mapping for " + typeof(TEntity).FullName);
}
await using var bills = new BillService.API.Data.BillDbContext(
    new DbContextOptionsBuilder<BillService.API.Data.BillDbContext>().UseSqlServer(testConnection).Options);
CheckLock<BillService.API.Models.Bill>(bills, "[purchases].[Bills]", "Id");
CheckLock<BillService.API.Models.BillNumberCounter>(bills, "[purchases].[BillNumberCounters]", "PeriodKey");
await using var orders = new PurchaseOrderService.API.Data.PurchaseOrderDbContext(
    new DbContextOptionsBuilder<PurchaseOrderService.API.Data.PurchaseOrderDbContext>().UseSqlServer(testConnection).Options);
CheckLock<PurchaseOrderService.API.Models.PurchaseOrder>(orders, "[purchases].[PurchaseOrders]", "Id");
CheckLock<PurchaseOrderService.API.Models.PurchaseOrderNumberCounter>(orders, "[purchases].[PurchaseOrderNumberCounters]", "PeriodKey");
await using var payments = new PaymentMadeService.API.Data.PaymentMadeDbContext(
    new DbContextOptionsBuilder<PaymentMadeService.API.Data.PaymentMadeDbContext>().UseSqlServer(testConnection).Options);
CheckLock<PaymentMadeService.API.Models.PaymentBillLookup>(payments, "[purchases].[Bills]", "Id");
CheckLock<PaymentMadeService.API.Models.PaymentMade>(payments, "[purchases].[PaymentsMade]", "Id");
CheckLock<PaymentMadeService.API.Models.PaymentMadeNumberCounter>(payments, "[purchases].[PaymentMadeNumberCounters]", "PeriodKey");
await using var credits = new VendorCreditService.API.Data.VendorCreditDbContext(
    new DbContextOptionsBuilder<VendorCreditService.API.Data.VendorCreditDbContext>().UseSqlServer(testConnection).Options);
CheckLock<VendorCreditService.API.Models.VendorCredit>(credits, "[purchases].[VendorCredits]", "Id");
CheckLock<VendorCreditService.API.Models.VendorCreditNumberCounter>(credits, "[purchases].[VendorCreditNumberCounters]", "PeriodKey");
CheckLock<VendorCreditService.API.Models.PurchaseBillLookup>(credits, "[purchases].[Bills]", "Id");
CheckLock<VendorCreditService.API.Models.VendorCreditApplication>(credits, "[purchases].[VendorCreditApplications]", "Id");
await using var sales = new SalesInvoiceService.API.Data.SalesInvoiceDbContext(
    new DbContextOptionsBuilder<SalesInvoiceService.API.Data.SalesInvoiceDbContext>().UseSqlServer(testConnection).Options);
CheckLock<SalesInvoiceService.API.Models.SalesInvoice>(sales, "[sales].[SalesInvoices]", "Id");
CheckLock<SalesInvoiceService.API.Models.InvoiceNumberCounter>(sales, "[sales].[InvoiceNumberCounters]", "PeriodKey");
CheckLock<SalesInvoiceService.API.Models.SalesCreditNote>(sales, "[sales].[SalesCreditNotes]", "Id");
CheckLock<SalesInvoiceService.API.Models.CreditNoteNumberCounter>(sales, "[sales].[CreditNoteNumberCounters]", "PeriodKey");
Console.WriteLine($"{checks} real-model lock checks passed without contacting a database.");
