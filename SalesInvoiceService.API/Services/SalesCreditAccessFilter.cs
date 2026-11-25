using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SalesInvoiceService.API.Data;
using SalesInvoiceService.API.DTOs;
using SparkFinance.Http;

namespace SalesInvoiceService.API.Services
{
    public sealed class SalesCreditAccessFilter(
        FinanceCompanyAccess access,
        SalesInvoiceDbContext database) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            var scope = await access.GetAsync(http.Request, "/sales/credit-notes", http.RequestAborted);
            if (scope.Status != 200 || scope.Branches is { Length: 0 })
            {
                context.Result = new StatusCodeResult(scope.Status == 200 ? 403 : scope.Status);
                return;
            }
            http.Items[typeof(FinanceCompanyScope)] = scope;
            var action = context.ActionDescriptor.RouteValues["action"];
            var write = http.Request.Method != "GET";
            var actionBranches = action switch
            {
                "Preview" or "Create" => scope.CreatableBranches,
                "Issue" => scope.IssuableBranches,
                _ => scope.EditableBranches
            };
            var branches = new List<string>();
            if (context.ActionArguments.TryGetValue("id", out var id) && id is int creditId)
            {
                var branch = await database.SalesCreditNotes.AsNoTracking()
                    .Where(credit => credit.Id == creditId)
                    .Select(credit => credit.SalesInvoice.Branch)
                    .SingleOrDefaultAsync(http.RequestAborted);
                if (branch == null)
                {
                    context.Result = new NotFoundResult();
                    return;
                }
                branches.Add(branch);
            }
            foreach (var dto in context.ActionArguments.Values.OfType<CreateSalesCreditNoteDto>())
            {
                var branch = await database.SalesInvoices.AsNoTracking()
                    .Where(invoice => invoice.Id == dto.SalesInvoiceId)
                    .Select(invoice => invoice.Branch).SingleOrDefaultAsync(http.RequestAborted);
                branches.Add(branch ?? string.Empty);
            }
            foreach (var dto in context.ActionArguments.Values.OfType<ApplyCustomerCreditDto>())
            {
                var invoiceBranches = await database.SalesInvoices.AsNoTracking()
                    .Where(invoice => invoice.Id == dto.SourceInvoiceId || invoice.Id == dto.TargetInvoiceId)
                    .Select(invoice => invoice.Branch).ToListAsync(http.RequestAborted);
                if (invoiceBranches.Count != 2)
                {
                    context.Result = new NotFoundResult();
                    return;
                }
                branches.AddRange(invoiceBranches);
            }
            if (context.ActionArguments.TryGetValue("branch", out var selectedBranch) && selectedBranch is string name && !string.IsNullOrWhiteSpace(name))
            {
                branches.Add(name);
            }
            if (branches.Any(branch => write ? !scope.AllowsAction(branch, actionBranches) : !scope.Allows(branch)))
            {
                context.Result = new StatusCodeResult(403);
                return;
            }
            await next();
        }
    }
}
