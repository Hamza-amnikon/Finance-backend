using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SparkFinance.Http;
using VendorCreditService.API.Data;
using VendorCreditService.API.DTOs;

namespace VendorCreditService.API.Services
{
    public sealed class VendorCreditAccessFilter(
        FinanceCompanyAccess access,
        VendorCreditDbContext database) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            var scope = await access.GetAsync(http.Request, "/purchases/vendor-credits", http.RequestAborted);
            if (scope.Status != 200 || scope.Branches is { Length: 0 })
            {
                context.Result = new StatusCodeResult(scope.Status == 200 ? 403 : scope.Status);
                return;
            }

            http.Items[typeof(FinanceCompanyScope)] = scope;
            foreach (var query in context.ActionArguments.Values.OfType<VendorCreditQueryDto>())
            {
                query.AllowedBranches = scope.Branches;
            }

            var action = context.ActionDescriptor.RouteValues["action"];
            var dto = context.ActionArguments.Values.OfType<VendorCreditFieldsDto>().FirstOrDefault();
            var branches = action switch
            {
                "Create" or "Preview" => scope.CreatableBranches,
                "Issue" => scope.IssuableBranches,
                "Cancel" => scope.CancellableBranches,
                "Void" => scope.VoidableBranches,
                _ => scope.EditableBranches
            };
            var write = http.Request.Method != "GET";
            var target = context.ActionArguments.TryGetValue("creditId", out var creditId) ? creditId :
                context.ActionArguments.TryGetValue("id", out var id) ? id : null;
            if (target is int recordId)
            {
                var recordBranch = await database.VendorCredits.AsNoTracking()
                    .Where(credit => credit.Id == recordId).Select(credit => credit.Branch)
                    .SingleOrDefaultAsync(http.RequestAborted);
                if (recordBranch == null || !scope.Allows(recordBranch))
                {
                    context.Result = new NotFoundResult();
                    return;
                }
                if (write && !scope.AllowsAction(recordBranch, branches))
                {
                    context.Result = new StatusCodeResult(403);
                    return;
                }
            }
            if (dto != null && !scope.AllowsAction(dto.Branch, branches))
            {
                context.Result = new StatusCodeResult(403);
                return;
            }
            await next();
        }
    }
}
