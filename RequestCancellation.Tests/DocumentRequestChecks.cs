using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using PurchaseOrderService.API.Controllers;
using PurchaseOrderService.API.Data;
using PurchaseOrderService.API.DTOs;
using PurchaseOrderService.API.Services;
using SalesEstimateService.API.Controllers;
using SalesEstimateService.API.DTOs;
using SalesEstimateService.API.Services;
using SalesInvoiceService.API.Controllers;
using SalesInvoiceService.API.Services;

internal static class DocumentRequestChecks
{
    public static async Task RunAsync(
        Exception sqlCancellation,
        Exception sqlFailure,
        CancellationToken cancelledToken)
    {
        var invoiceService = FailingService<ISalesInvoiceService>(sqlCancellation);
        var invoices = new SalesInvoicesController(null!, null!, invoiceService, null!);

        CheckCancelled(await invoices.GetAll(cancelledToken), "Invoice list");
        CheckCancelled(await invoices.GetById(1, cancelledToken), "Invoice details");

        try
        {
            await invoices.GetAll(CancellationToken.None);
            throw new InvalidOperationException("An active invoice request hid a SQL failure.");
        }
        catch (Exception exception)
            when (ReferenceEquals(exception, sqlCancellation))
        {
            Console.WriteLine("PASS: Active invoice requests preserve SQL errors.");
        }

        var estimates = new SalesEstimatesController(
            FailingService<ISalesEstimateService>(sqlCancellation),
            null!,
            NullLogger<SalesEstimatesController>.Instance);

        CheckCancelled(await estimates.GetAll(new SalesEstimateQueryDto(), cancelledToken), "Estimate list");
        CheckCancelled(await estimates.GetById(1, cancelledToken), "Estimate details");

        var failedEstimates = new SalesEstimatesController(
            FailingService<ISalesEstimateService>(sqlFailure),
            null!,
            NullLogger<SalesEstimatesController>.Instance);
        var result = await failedEstimates.GetAll(new SalesEstimateQueryDto(), cancelledToken);

        if (result is not ObjectResult { StatusCode: 503 })
        {
            throw new InvalidOperationException("A genuine estimate SQL error lost its existing 503 response.");
        }

        Console.WriteLine("PASS: Genuine estimate database errors keep their existing 503 response.");

        var orders = new PurchaseOrdersController(
            FailingService<IPurchaseOrderService>(new TaskCanceledException()),
            null!,
            NullLogger<PurchaseOrdersController>.Instance);

        CheckCancelled(await orders.GetPaged(new PurchaseOrderQueryDto(), cancelledToken), "Purchase order list");
        CheckCancelled(await orders.GetById(1, cancelledToken), "Purchase order details");

        var options = new DbContextOptionsBuilder<PurchaseOrderDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        await using var context = new PurchaseOrderDbContext(options);
        var summary = new PurchaseOrdersController(
            null!,
            context,
            NullLogger<PurchaseOrdersController>.Instance);

        CheckCancelled(await summary.Summary(cancelledToken), "Purchase order summary");
    }

    private static T FailingService<T>(Exception exception)
        where T : class
    {
        var service = DispatchProxy.Create<T, FailingServiceProxy>();
        ((FailingServiceProxy)(object)service).Failure = exception;
        return service;
    }

    private static void CheckCancelled(IActionResult result, string description)
    {
        if (result is not StatusCodeResult { StatusCode: 499 })
        {
            throw new InvalidOperationException(description + " did not handle the cancelled SQL request.");
        }

        Console.WriteLine("PASS: " + description + " handles cancelled SQL requests.");
    }
}

public class FailingServiceProxy : DispatchProxy
{
    public Exception Failure { get; set; } = null!;

    protected override object? Invoke(MethodInfo? method, object?[]? arguments)
    {
        var resultType = method!.ReturnType.GetGenericArguments().Single();
        var factory = typeof(Task).GetMethods()
            .Single(value => value.Name == nameof(Task.FromException) && value.IsGenericMethodDefinition);

        return factory.MakeGenericMethod(resultType).Invoke(null, [Failure]);
    }
}
