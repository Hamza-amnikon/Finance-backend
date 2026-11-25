using ExpenseService.API.Data;
using ExpenseService.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SparkFinance.Http;

namespace ExpenseService.API.Services
{
    public sealed class ExpenseClaimAccessFilter(
        FinanceCompanyAccess access,
        ExpenseDbContext database) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            var scope = await access.GetAsync(http.Request, "/expenses/claims", http.RequestAborted);
            if (scope.Status != 200)
            {
                context.Result = new StatusCodeResult(scope.Status);
                return;
            }
            if (string.IsNullOrWhiteSpace(scope.ActorId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }
            if (scope.CompanyIds is { Length: 0 })
            {
                context.Result = new StatusCodeResult(403);
                return;
            }

            http.Items[typeof(ExpenseClaimAccessFilter)] = scope;
            var action = context.ActionDescriptor.RouteValues["action"];
            var dto = context.ActionArguments.Values.OfType<ExpenseClaimDto>().FirstOrDefault();
            var transition = context.ActionArguments.Values.OfType<ExpenseClaimActionDto>().FirstOrDefault();
            var companyIds = action == "Create" ? scope.CreatableCompanyIds : scope.EditableCompanyIds;
            if (transition != null)
            {
                companyIds = transition.Status switch
                {
                    "Submitted" => scope.IssuableCompanyIds,
                    "Cancelled" => scope.CancellableCompanyIds,
                    "Approved" or "Rejected" => scope.ApprovableCompanyIds,
                    "Paid" => scope.PostableCompanyIds,
                    "Draft" => scope.EditableCompanyIds,
                    _ => []
                };
            }
            var write = http.Request.Method != "GET";
            if (context.ActionArguments.TryGetValue("id", out var value) && value is int id)
            {
                var companyId = await database.ExpenseClaims.AsNoTracking()
                    .Where(claim => claim.Id == id).Select(claim => (int?)claim.CompanyId)
                    .SingleOrDefaultAsync(http.RequestAborted);
                if (!companyId.HasValue || !scope.AllowsCompany(companyId.Value))
                {
                    context.Result = new NotFoundResult();
                    return;
                }
                if (write && !scope.AllowsCompanyAction(companyId.Value, companyIds))
                {
                    context.Result = new StatusCodeResult(403);
                    return;
                }
            }
            if (dto != null && !scope.AllowsCompanyAction(dto.CompanyId, companyIds))
            {
                context.Result = new StatusCodeResult(403);
                return;
            }
            if (context.ActionArguments.TryGetValue("companyId", out var selected) &&
                selected is int selectedId && !scope.AllowsCompany(selectedId))
            {
                context.Result = new StatusCodeResult(403);
                return;
            }
            await next();
        }
    }
}
