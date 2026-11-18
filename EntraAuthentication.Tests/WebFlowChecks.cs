using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using RoleService.API.DTOs;
using RoleService.API.Models;
using RoleService.API.Authentication;
using System.Security.Cryptography;

internal static class WebFlowChecks
{
    public static async Task RunAsync(
        HttpClient client,
        IServiceProvider services,
        WebTokenEndpoint tokenEndpoint,
        User user,
        SecurityKey signingKey,
        string issuer,
        Guid clientId,
        HttpStatusCode expectedAdminEndpointStatus = HttpStatusCode.Unauthorized)
    {
        async Task<HttpResponseMessage> CompleteLogin(bool badNonce = false, bool badState = false)
        {
            client.DefaultRequestHeaders.Clear();
            using var challenge = await client.GetAsync("api/auth/login?returnUrl=https://untrusted.example/");
            Require(challenge.StatusCode == HttpStatusCode.Redirect, "Web login challenges the identity provider.");
            Require(challenge.Headers.GetValues("Set-Cookie").Any(value => value.StartsWith(MeritJwtCookie.Name + "=;")),
                "A new Microsoft sign-in clears any previous MERIT JWT before authentication.");
            var query = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
            Require(query["response_type"] == "code" && query.ContainsKey("code_challenge"),
                "Web login uses authorization code flow with PKCE.");
            Require(!query.ContainsKey("client_secret"), "The client secret never enters the browser redirect.");
            Require(query["redirect_uri"].ToString().EndsWith("/signin-oidc"),
                "The callback targets Role Service instead of the frontend.");
            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                issuer,
                clientId.ToString(),
                [
                    new Claim("sub", user.ObjectId.ToString()),
                    new Claim("tid", user.TenantId.ToString()),
                    new Claim("oid", user.ObjectId.ToString()),
                    new Claim("nonce", badNonce ? "invalid-nonce" : query["nonce"].ToString()),
                    new Claim("roles", "Admin"),
                    new Claim("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
                ],
                now.AddMinutes(-1),
                now.AddMinutes(10),
                new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));
            tokenEndpoint.IdToken = new JwtSecurityTokenHandler().WriteToken(token);
            client.DefaultRequestHeaders.Add("Cookie", string.Join("; ",
                challenge.Headers.GetValues("Set-Cookie").Select(cookie => cookie.Split(';')[0])));

            return await client.PostAsync("signin-oidc", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = "test-authorization-code",
                ["state"] = badState ? "invalid-state" : query["state"].ToString()
            }));
        }

        using var callback = await CompleteLogin();
        Require(callback.StatusCode == HttpStatusCode.Redirect &&
            callback.Headers.Location?.ToString() == "http://localhost:5173/",
            "Only the configured frontend can receive the completed sign-in.");
        Require(tokenEndpoint.RequestBody.Contains("client_secret=isolated-test-credential") &&
            tokenEndpoint.RequestBody.Contains("code_verifier="),
            "The backend alone exchanges the authorization code using its credential and PKCE verifier.");
        var sessionHeader = callback.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(MeritJwtCookie.Name + "="));
        Require(sessionHeader.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
            sessionHeader.Contains("secure", StringComparison.OrdinalIgnoreCase),
            "The Microsoft session cookie is HttpOnly and Secure.");
        Require(!sessionHeader.Contains(tokenEndpoint.IdToken), "The Microsoft ID token is not used as the MERIT session token.");
        var sessionCookie = sessionHeader.Split(';')[0];
        var meritToken = sessionCookie[(MeritJwtCookie.Name.Length + 1)..];
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(meritToken);
        Require(parsed.Issuer == "MERIT-test" && parsed.Audiences.Single() == "MERIT-test" &&
            parsed.Subject == user.UserId.ToString() && parsed.Claims.Single(claim => claim.Type == "role_id").Value ==
            user.RoleId.ToString() && parsed.Header.Alg == SecurityAlgorithms.RsaSha256 &&
            parsed.ValidTo > DateTime.UtcNow && parsed.Claims.Any(claim => claim.Type == "jti"),
            "The backend issues a signed, expiring MERIT JWT with the existing user and role 14.");
        Require(callback.Headers.Location!.Query.Length == 0 && callback.Headers.Location.Fragment.Length == 0,
            "The JWT never appears in the redirect URL.");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + meritToken);
        Require((await client.GetAsync("api/auth/session")).StatusCode == HttpStatusCode.OK,
            "Role Service validates its MERIT JWT as a bearer token as well as through the HttpOnly cookie.");
        Require((await client.GetAsync("test/admin")).StatusCode == HttpStatusCode.Forbidden,
            "The MERIT JWT for role 14 does not gain Admin access.");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Cookie", sessionCookie);
        using var session = await client.GetAsync("api/auth/session");
        var data = await session.Content.ReadFromJsonAsync<UserSessionResponse>();
        Require(session.StatusCode == HttpStatusCode.OK && data?.Role == "Test CA",
            "Web sessions use the database role and ignore the token's Admin claim.");
        await MeritJwtChecks.RunAsync(client, services, user, meritToken, sessionCookie);
        Require((await client.GetAsync("api/development-admin/session")).StatusCode == expectedAdminEndpointStatus,
            "Microsoft cookies cannot enter the development Admin session.");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer invalid");
        Require((await client.GetAsync("api/auth/session")).StatusCode == HttpStatusCode.Unauthorized,
            "Invalid bearer tokens cannot fall back to the Microsoft cookie.");
        client.DefaultRequestHeaders.Remove("Authorization");
        using var missingCsrf = await client.PostAsync("api/auth/logout", null);
        Require(missingCsrf.StatusCode == HttpStatusCode.BadRequest,
            "Cookie logout rejects requests without an antiforgery token: " +
            missingCsrf.StatusCode + " " + await missingCsrf.Content.ReadAsStringAsync());
        using var csrf = await client.GetAsync("api/auth/csrf");
        var csrfData = await csrf.Content.ReadFromJsonAsync<CsrfResponse>();
        var antiforgeryCookie = csrf.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", sessionCookie + "; " + antiforgeryCookie);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrfData!.Token);
        using var logout = await client.PostAsync("api/auth/logout", null);
        Require(logout.StatusCode == HttpStatusCode.NoContent &&
            logout.Headers.GetValues("Set-Cookie").Any(value => value.StartsWith(MeritJwtCookie.Name + "=;")),
            "Verified logout removes only the local Microsoft session.");

        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Cookie", sessionCookie);
        user.IsActive = false;
        Require((await client.GetAsync("api/auth/session")).StatusCode == HttpStatusCode.Unauthorized,
            "An existing Microsoft cookie stops working when its user is deactivated.");
        using var rejected = await CompleteLogin();
        Require(rejected.Headers.Location?.Query.Contains("ssoError=") == true &&
            !rejected.Headers.GetValues("Set-Cookie").Any(value => value.StartsWith(MeritJwtCookie.Name + "=")),
            "Inactive users cannot receive a Microsoft session after a valid identity-provider callback.");
        user.IsActive = true;
        using var nonceFailure = await CompleteLogin(badNonce: true);
        Require(nonceFailure.Headers.Location?.Query.Contains("ssoError=") == true,
            "A mismatched ID-token nonce is rejected.");
        using var stateFailure = await CompleteLogin(badState: true);
        Require(stateFailure.Headers.Location?.Query.Contains("ssoError=") == true,
            "A forged callback state is rejected.");
        var configuration = services.GetRequiredService<IConfiguration>();
        configuration["AzureAd:ClientSecret"] = "";
        client.DefaultRequestHeaders.Clear();
        Require((await client.GetAsync("api/auth/login")).StatusCode == HttpStatusCode.ServiceUnavailable,
            "Missing server credentials fail only Microsoft login, without changing the Admin setup.");
        configuration["AzureAd:ClientSecret"] = "isolated-test-credential";
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }

        Console.WriteLine("PASS: " + message);
    }

    private sealed record CsrfResponse(string Token);
}

internal sealed class WebTokenEndpoint : HttpMessageHandler
{
    public string IdToken { get; set; } = string.Empty;

    public string RequestBody { get; private set; } = string.Empty;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id_token = IdToken,
                access_token = "test-server-only-access-token",
                token_type = "Bearer",
                expires_in = 600
            })
        };
    }
}
