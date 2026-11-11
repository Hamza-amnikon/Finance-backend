using Microsoft.AspNetCore.Mvc;
using SalesInvoiceService.API.Controllers;
using SalesInvoiceService.API.Models;
using SalesInvoiceService.API.Services;

internal static class PaymentModeCancellationChecks
{
    public static async Task RunAsync()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var service = new StubService(token => Task.FromCanceled<List<PaymentMode>>(token));
        var result = await new PaymentModesController(service).GetActive(cancelled.Token);
        Check(result is StatusCodeResult { StatusCode: 499 },
            "Cancelled payment-mode requests are handled without an unhandled exception");

        var modes = new List<PaymentMode>();
        service = new StubService(token => Task.FromResult(modes));
        result = await new PaymentModesController(service).GetActive(default);
        Check(result is OkObjectResult response && ReferenceEquals(response.Value, modes),
            "Successful payment-mode requests return their original result");

        service = new StubService(token => Task.FromException<List<PaymentMode>>(new TaskCanceledException()));
        await MustPropagateAsync<TaskCanceledException>(service,
            "Unexpected cancellations are not mistaken for a disconnected client");

        service = new StubService(token => Task.FromException<List<PaymentMode>>(new InvalidOperationException("Database unavailable")));
        await MustPropagateAsync<InvalidOperationException>(service,
            "Database failures are not hidden as successful empty lookups");
    }

    private static void Check(bool condition, string description)
    {
        if (!condition)
        {
            throw new Exception("FAIL: " + description);
        }
        Console.WriteLine("PASS: " + description);
    }

    private static async Task MustPropagateAsync<T>(IPaymentModeService service, string description)
        where T : Exception
    {
        try
        {
            await new PaymentModesController(service).GetActive(default);
        }
        catch (T)
        {
            Check(true, description);
            return;
        }
        throw new Exception("FAIL: " + description);
    }

    private sealed class StubService : IPaymentModeService
    {
        private readonly Func<CancellationToken, Task<List<PaymentMode>>> _load;

        public StubService(Func<CancellationToken, Task<List<PaymentMode>>> load)
        {
            _load = load;
        }

        public Task<List<PaymentMode>> GetActiveAsync(CancellationToken cancellationToken)
        {
            return _load(cancellationToken);
        }

        public Task<PaymentMode?> GetByNameAsync(string name, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
