using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace SparkFinance.Http;

public interface IFinanceBranchQuery
{
    string[]? AllowedBranches { get; set; }
}

public interface IFinanceBranchRecord
{
    string Branch { get; }
}

public interface IFinanceCompanyQuery
{
    int[]? AllowedCompanyIds { get; set; }
}

public interface IFinanceCompanyRecord
{
    int CompanyId { get; }
}

public interface IFinanceTransferRecord
{
    int SourceCompanyId { get; }
    int DestinationCompanyId { get; }
}

public sealed class FinanceBranchReadAttribute : TypeFilterAttribute
{
    public FinanceBranchReadAttribute(string route, bool detail = false, bool companyScoped = false) : base(typeof(FinanceBranchReadFilter))
    {
        Arguments = [route, detail, companyScoped];
    }
}

public sealed class FinanceBranchReadFilter(
    string route, bool detail, bool companyScoped, FinanceCompanyAccess access, IConfiguration configuration) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (configuration["Database:Provider"] != "MySql")
        {
            await next();
            return;
        }

        var http = context.HttpContext;
        http.Response.Headers.CacheControl = "no-store";
        var scope = await access.GetAsync(http.Request, route, http.RequestAborted);
        if (scope.Status != 200)
        {
            context.Result = new StatusCodeResult(scope.Status);
            return;
        }

        if (!detail)
        {
            if (companyScoped)
            {
                var query = context.ActionArguments.Values.OfType<IFinanceCompanyQuery>().Single();
                query.AllowedCompanyIds = scope.CompanyIds;
            }
            else
            {
                var query = context.ActionArguments.Values.OfType<IFinanceBranchQuery>().Single();
                query.AllowedBranches = scope.Branches;
            }
            await next();
            return;
        }

        var executed = await next();
        if (executed.Result is OkObjectResult result && !IsAllowed(result.Value, scope))
        {
            executed.Result = new NotFoundResult();
        }
    }

    private bool IsAllowed(object? record, FinanceCompanyScope scope) => record switch
    {
        IFinanceCompanyRecord company when companyScoped => scope.AllowsCompany(company.CompanyId),
        IFinanceTransferRecord transfer when companyScoped => scope.AllowsCompany(transfer.SourceCompanyId) &&
            scope.AllowsCompany(transfer.DestinationCompanyId),
        IFinanceBranchRecord branch when !companyScoped => scope.Allows(branch.Branch),
        _ => false
    };
}
