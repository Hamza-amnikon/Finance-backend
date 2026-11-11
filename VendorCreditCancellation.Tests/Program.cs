using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VendorCreditService.API.Controllers;
using VendorCreditService.API.DTOs;
using VendorCreditService.API.Models;
using VendorCreditService.API.Services;

var checks = 0;
void Check(bool condition, string description)
{
    if (!condition)
    {
        throw new Exception("FAIL: " + description);
    }
    checks++;
    Console.WriteLine("PASS: " + description);
}
using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
var service = new CreditStub
{
    Failure = token => new OperationCanceledException(token)
};
VendorCreditsController Controller()
{
    return new VendorCreditsController(service, null!, NullLogger<VendorCreditsController>.Instance);
}
Check(await Controller().GetPaged(new VendorCreditQueryDto(), cancelled.Token) is StatusCodeResult { StatusCode: 499 },
    "Cancelled vendor credit lists return 499 without an unhandled exception");
Check(await Controller().GetById(1, cancelled.Token) is StatusCodeResult { StatusCode: 499 },
    "Cancelled vendor credit details return 499");
service.Failure = null;
Check(await Controller().GetPaged(new VendorCreditQueryDto(), default) is OkObjectResult,
    "Successful vendor credit lists retain their normal response");
service.Failure = token => new TaskCanceledException();
try
{
    await Controller().GetPaged(new VendorCreditQueryDto(), default);
    throw new Exception("FAIL: Unexpected cancellation was hidden");
}
catch (TaskCanceledException)
{
    Check(true, "Unexpected service cancellation is not mistaken for a disconnected client");
}
service.Failure = token => new InvalidOperationException("Existing business conflict");
Check(await Controller().GetPaged(new VendorCreditQueryDto(), default) is ConflictObjectResult,
    "Existing business conflict handling remains unchanged");
var applications = new CreditApplicationsController(new ApplicationStub(), NullLogger<CreditApplicationsController>.Instance);
Check(await applications.Bills(1, cancelled.Token) is StatusCodeResult { StatusCode: 499 },
    "Cancelled credit application bill lookups return 499");
Console.WriteLine($"All {checks} cancellation regression checks passed.");

sealed class CreditStub : IVendorCreditService
{
    public Func<CancellationToken, Exception>? Failure { get; set; }

    public Task<(List<VendorCreditResponseDto> Data, int Total)> GetPagedAsync(VendorCreditQueryDto query, CancellationToken token)
    {
        return Failure == null
            ? Task.FromResult((new List<VendorCreditResponseDto>(), 0))
            : Task.FromException<(List<VendorCreditResponseDto>, int)>(Failure(token));
    }

    public Task<VendorCreditResponseDto?> GetByIdAsync(int id, CancellationToken token)
    {
        return Task.FromException<VendorCreditResponseDto?>(Failure!(token));
    }

    public Task<VendorCreditResponseDto> PreviewAsync(VendorCreditFieldsDto dto, CancellationToken token)
    {
        throw new NotSupportedException();
    }

    public Task<VendorCreditResponseDto> CreateAsync(CreateVendorCreditDto dto, CancellationToken token)
    {
        throw new NotSupportedException();
    }

    public Task<VendorCreditResponseDto?> UpdateAsync(int id, UpdateVendorCreditDto dto, CancellationToken token)
    {
        throw new NotSupportedException();
    }

    public Task<VendorCreditResponseDto?> ChangeStatusAsync(int id, VendorCreditStatus status, byte[] rowVersion, CancellationToken token)
    {
        throw new NotSupportedException();
    }
}

sealed class ApplicationStub : ICreditApplicationService
{
    public Task<(List<CreditBillBalanceDto> Data, int Total)> GetBillsAsync(int creditId, string? search, int page, int pageSize, CancellationToken token)
    {
        return Task.FromCanceled<(List<CreditBillBalanceDto>, int)>(token);
    }

    public Task<CreditApplicationResponseDto> ApplyAsync(int creditId, ApplyVendorCreditDto dto, CancellationToken token)
    {
        throw new NotSupportedException();
    }

    public Task<CreditApplicationResponseDto?> VoidAsync(int creditId, int applicationId, VoidCreditApplicationDto dto, CancellationToken token)
    {
        throw new NotSupportedException();
    }
}
