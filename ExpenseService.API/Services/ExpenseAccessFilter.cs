using ExpenseService.API.Data;
using ExpenseService.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SparkFinance.Http;

namespace ExpenseService.API.Services
{
    public sealed class ExpenseAccessFilter(
        FinanceCompanyAccess access,
        ExpenseDbContext database) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var request = context.HttpContext.Request;
            var cancellationToken = context.HttpContext.RequestAborted;
            var actionName = context.ActionDescriptor.RouteValues["action"];
            if (actionName is "Countries" or "Currencies")
            {
                await next();
                return;
            }

            string? lookupRoute = null;
            if (actionName == "LookupCompanySetup")
            {
                context.ActionArguments.TryGetValue("usage", out var lookupUsage);
                lookupRoute = (lookupUsage as string) switch
                {
                    "journals" => "/accounting/journal-entries",
                    "role-permissions" => "/settings/role-permissions",
                    "branch-comparison" => "/reports/branch-comparison",
                    _ => null
                };
                if (lookupRoute == null)
                {
                    context.Result = new BadRequestResult();
                    return;
                }
            }

            var organization = actionName is "CompanySetup" or "CreateCompanySetup" or "AddCompany" or "AddAccount";
            var scope = await access.GetAsync(request,
                lookupRoute ?? (organization ? "/settings/organization" : "/expenses"), cancellationToken);
            if (scope.Status != 200 || scope.CompanyIds is { Length: 0 })
            {
                context.Result = new StatusCodeResult(scope.Status == 200 ? 403 : scope.Status);
                return;
            }

            foreach (var query in context.ActionArguments.Values.OfType<ExpenseQueryDto>())
            {
                query.AllowedCompanyIds = scope.CompanyIds;
            }

            var dto = context.ActionArguments.Values.OfType<ExpenseDto>().FirstOrDefault();
            int? companyId = dto?.CompanyId;
            if (context.ActionArguments.Values.OfType<BankAccountDto>().FirstOrDefault() is { } account)
            {
                companyId = account.CompanyId;
            }
            if (context.ActionArguments.TryGetValue("id", out var value) && value is int id)
            {
                var existingCompany = await database.Expenses.AsNoTracking()
                    .Where(expense => expense.Id == id)
                    .Select(expense => (int?)expense.CompanyId)
                    .SingleOrDefaultAsync(cancellationToken);
                if (!existingCompany.HasValue || !scope.AllowsCompany(existingCompany.Value))
                {
                    context.Result = new NotFoundResult();
                    return;
                }

                companyId ??= existingCompany;
                if (request.Method != "GET" &&
                    !scope.AllowsCompanyAction(existingCompany.Value,
                        actionName == "Void" ? scope.VoidableCompanyIds : scope.EditableCompanyIds))
                {
                    context.Result = new StatusCodeResult(403);
                    return;
                }
            }

            if (companyId.HasValue)
            {
                var permitted = request.Method == "GET"
                    ? scope.AllowsCompany(companyId.Value)
                    : scope.AllowsCompanyAction(companyId.Value,
                        actionName == "Void" ? scope.VoidableCompanyIds :
                        organization ? scope.CreatableCompanyIds :
                        request.Method == "POST" && actionName == "Create" ? scope.CreatableCompanyIds : scope.EditableCompanyIds);
                if (!permitted || (dto?.Status == "Recorded" &&
                    !scope.AllowsCompanyAction(companyId.Value, scope.PostableCompanyIds)))
                {
                    context.Result = new StatusCodeResult(403);
                    return;
                }
            }
            else if (request.Method != "GET" && scope.CompanyIds != null &&
                !(organization && scope.CreatableCompanyIds.Length > 0))
            {
                context.Result = new StatusCodeResult(403);
                return;
            }

            var executed = await next();
            if (executed.Result is ObjectResult result && scope.CompanyIds != null)
            {
                if (result.Value is IEnumerable<global::ExpenseService.API.Models.Company> companies)
                {
                    result.Value = companies.Where(company => scope.AllowsCompany(company.Id)).ToList();
                }
                else if (result.Value is IEnumerable<global::ExpenseService.API.Models.BankAccount> accounts)
                {
                    result.Value = accounts.Where(account => scope.AllowsCompany(account.CompanyId)).ToList();
                }
                else if (result.Value is IEnumerable<CompanySetupResponse> setups)
                {
                    result.Value = setups.Where(setup => scope.AllowsCompany(setup.CompanyId)).ToList();
                }
            }
        }
    }
}
