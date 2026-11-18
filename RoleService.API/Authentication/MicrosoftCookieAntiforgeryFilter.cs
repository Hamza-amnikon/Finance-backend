using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RoleService.API.Authentication
{
    public class MicrosoftCookieAntiforgeryFilter : IAsyncAuthorizationFilter
    {
        private readonly IAntiforgery _antiforgery;

        public MicrosoftCookieAntiforgeryFilter(IAntiforgery antiforgery)
        {
            _antiforgery = antiforgery;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var request = context.HttpContext.Request;
            var authenticationType = context.HttpContext.User.Identity?.AuthenticationType;

            if ((authenticationType != FinanceAuthenticationSchemes.MicrosoftCookie &&
                authenticationType != FinanceAuthenticationSchemes.MeritCookie) ||
                HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) ||
                HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method))
            {
                return;
            }

            if (!await _antiforgery.IsRequestValidAsync(context.HttpContext))
            {
                context.Result = new BadRequestObjectResult(new
                {
                    message = "Refresh the page and retry the request with its CSRF token."
                });
            }
        }
    }
}
