using BankingService.API.DTOs;
using BankingService.API.Models;
using BankingService.API.Services;

var requester = new InternalPaymentRequestScope(false, [12], [12], [12], [], "requester");
var approver = new InternalPaymentRequestScope(false, [31], [], [], [31], "approver");
var viewer = new InternalPaymentRequestScope(false, [31], [], [], [], "viewer");
var admin = new InternalPaymentRequestScope(true, [], [], [], [], "admin");
var request = new InternalPaymentRequest
{
    CreatedBy = "requester",
    RequestingCompanyId = 12,
    PayingCompanyId = 31
};

Check(requester.CanCreate(12) && !requester.CanCreate(31), "Create permission is scoped to the requesting company.");
Check(!requester.CanApprove(31), "Requester access does not imply payer approval access.");
Check(approver.CanApprove(31) && !approver.CanApprove(12), "Approve permission is scoped to the paying company.");
Check(!viewer.CanApprove(31), "View permission cannot approve a request.");
Check(admin.CanCreate(12) && admin.CanApprove(31), "Verified local admin retains both company actions.");
Check(!admin.CanView(0), "An invalid company ID is rejected even for admin.");

Check(InternalPaymentRequestService.GetNextStatus(request, "submit", "", requester) == "Submitted", "Requester can submit a draft.");
Reject(() => InternalPaymentRequestService.GetNextStatus(request, "approve", "", approver), 409, "A draft cannot be approved.");
request.Status = "Submitted";
Reject(() => InternalPaymentRequestService.GetNextStatus(request, "approve", "", requester), 403, "Requester cannot approve the payer's request.");
Reject(() => InternalPaymentRequestService.GetNextStatus(request, "approve", "", viewer), 403, "A payer viewer cannot approve.");
Check(InternalPaymentRequestService.GetNextStatus(request, "approve", "", approver) == "Approved", "Authorized payer can approve a submitted request.");
Reject(() => InternalPaymentRequestService.GetNextStatus(request, "reject", " ", approver), 400, "Rejection requires a reason.");
Check(InternalPaymentRequestService.GetNextStatus(request, "reject", "Incorrect budget", approver) == "Rejected", "Authorized payer can reject with a reason.");
Reject(() => InternalPaymentRequestService.GetNextStatus(request, "approve", "", admin with { ActorId = "requester" }), 403, "Even admin cannot approve their own request.");
Reject(() => InternalPaymentRequestService.GetNextStatus(request, "reject", "Reason", approver with { ActorId = "requester" }), 403, "Creator cannot reject their own request.");
Reject(() => InternalPaymentRequestService.GetNextStatus(request, "cancel", "", requester), 403, "Edit does not grant cancellation.");
Check(InternalPaymentRequestService.GetNextStatus(request, "cancel", "", requester with { CancellableCompanyIds = [12] }) == "Cancelled", "Explicit Cancel permission allows cancellation.");
Check(InternalPaymentRequestService.GetNextStatus(request, "approve", "", approver with { ActorId = "second-approver" }) == "Approved", "A second authorized approver can decide a pending request.");
request.Status = "Approved";
Reject(() => InternalPaymentRequestService.GetNextStatus(request, "cancel", "", admin), 409, "Approved requests cannot be silently cancelled.");
Reject(() => InternalPaymentRequestService.GetNextStatus(request, "approve", "", approver), 409, "An approval cannot be repeated.");

var lines = new List<InternalPaymentRequestLineDto>
{
    new() { Purpose = "Salary", Amount = 50000.25m },
    new() { Purpose = "Operating expenses", Amount = 1234.50m }
};
Check(InternalPaymentRequestService.ValidateLines(lines, 2) == 51234.75m, "Totals use exact decimal addition.");
Reject(() => InternalPaymentRequestService.ValidateLines([], 2), 400, "Empty requests are rejected.");
Reject(() => InternalPaymentRequestService.ValidateLines([new() { Purpose = "Salary", Amount = 1.001m }], 2), 400, "Excess currency precision is rejected without rounding.");
Reject(() => InternalPaymentRequestService.ValidateLines([new() { Purpose = "Salary", Amount = -1m }], 2), 400, "Negative amounts are rejected.");
Reject(() => InternalPaymentRequestService.ValidateLines([new() { Purpose = " ", Amount = 1m }], 2), 400, "Every line requires a purpose.");
Reject(() => InternalPaymentRequestService.ValidateLines([new() { Purpose = "Large", Amount = 99999999999999m }, new() { Purpose = "More", Amount = 2m }], 2), 400, "Oversized totals are rejected.");
var serialized = System.Text.Json.JsonSerializer.Serialize(new InternalPaymentRequest
{
    Amount = 99999999999999.99m,
    Lines = [new InternalPaymentRequestLine { Purpose = "Exact value", Amount = 99999999999999.99m }]
});
using var document = System.Text.Json.JsonDocument.Parse(serialized);
Check(document.RootElement.GetProperty("Amount").GetString() == "99999999999999.99",
    "Request totals retain exact decimal text in browser responses.");
Check(document.RootElement.GetProperty("Lines")[0].GetProperty("Amount").GetString() == "99999999999999.99",
    "Purpose amounts retain exact decimal text for editing.");
var input = System.Text.Json.JsonSerializer.Deserialize<InternalPaymentRequestLineDto>(
    "{\"Purpose\":\"Salary\",\"Amount\":\"123456.78\"}");
Check(input?.Amount == 123456.78m, "The API reads exact decimal strings from the form.");
Console.WriteLine("Internal payment request rule checks passed. No database records were changed.");

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception(message);
    }
    Console.WriteLine("PASS: " + message);
}

static void Reject(Action action, int status, string message)
{
    try
    {
        action();
    }
    catch (InternalPaymentRequestException exception) when (exception.Status == status)
    {
        Console.WriteLine("PASS: " + message);
        return;
    }
    throw new Exception(message);
}
