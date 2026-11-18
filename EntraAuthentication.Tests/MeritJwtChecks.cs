using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RoleService.API.Authentication;
using RoleService.API.DTOs;
using RoleService.API.Models;

internal static class MeritJwtChecks
{
    public static async Task RunAsync(
        HttpClient client,
        IServiceProvider services,
        User user,
        string validToken,
        string cookie)
    {
        var settings = services.GetRequiredService<IOptions<MeritJwtSettings>>().Value;
        var key = settings.CreateSigningKey()!;
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(validToken);

        string Issue(string? audience = null, string? issuer = null, bool expired = false,
            bool forgedSubject = false, SecurityKey? signingKey = null)
        {
            var claims = parsed.Claims.Where(claim => claim.Type is not ("exp" or "nbf" or "iat" or "iss" or "aud" or "sub" or "role"));
            var token = new JwtSecurityToken(
                issuer ?? settings.Issuer,
                audience ?? settings.Audience,
                claims.Concat([
                    new Claim("sub", forgedSubject ? Guid.NewGuid().ToString() : user.UserId.ToString()),
                    new Claim("role", "Admin")
                ]),
                DateTime.UtcNow.AddHours(-2),
                expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(5),
                new SigningCredentials(signingKey ?? key, SecurityAlgorithms.RsaSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        using var untrustedRsa = RSA.Create(2048);
        foreach (var invalid in new[]
        {
            Issue(audience: "wrong-audience"),
            Issue(issuer: "untrusted-issuer"),
            Issue(expired: true),
            Issue(forgedSubject: true),
            Issue(signingKey: new RsaSecurityKey(untrustedRsa))
        })
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", invalid);
            using var response = await client.GetAsync("api/auth/session");

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                throw new Exception("An invalid MERIT JWT was accepted or fell back to the valid cookie.");
            }
        }

        Console.WriteLine("PASS: MERIT JWT signatures, issuer, audience, expiry and user subject are verified without cookie fallback.");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Issue());
        using var forgedRoleResponse = await client.GetAsync("api/auth/session");
        var data = await forgedRoleResponse.Content.ReadFromJsonAsync<UserSessionResponse>();

        if (data?.RoleId != user.RoleId || data.Role == "Admin")
        {
            throw new Exception("A MERIT token role claim overrode the database assignment.");
        }

        Console.WriteLine("PASS: Current database role 14 overrides a token's Admin role claim.");
        client.DefaultRequestHeaders.Authorization = null;
        var roles = (TestRoleRepository)services.GetRequiredService<RoleService.API.Repositories.IRoleRepository>();
        roles.Active = false;
        using var inactiveRole = await client.GetAsync("api/auth/session");

        if (inactiveRole.StatusCode != HttpStatusCode.Unauthorized)
        {
            throw new Exception("An inactive database role left the MERIT JWT active.");
        }

        roles.Active = true;
        Console.WriteLine("PASS: Deactivating the assigned role blocks an existing MERIT JWT.");
        var roleId = user.RoleId;
        user.RoleId = roleId + 1;
        using var roleChangeResponse = await client.GetAsync("api/auth/session");
        var changed = await roleChangeResponse.Content.ReadFromJsonAsync<UserSessionResponse>();

        if (changed?.RoleId != user.RoleId)
        {
            throw new Exception("The JWT session did not reflect a changed role.");
        }

        user.RoleId = roleId;
        var objectId = user.ObjectId;
        user.ObjectId = Guid.NewGuid();
        using var oldIdentity = await client.GetAsync("api/auth/session");

        if (oldIdentity.StatusCode != HttpStatusCode.Unauthorized)
        {
            throw new Exception("A corrected Microsoft identity left the old JWT identity active.");
        }

        user.ObjectId = objectId;
        Console.WriteLine("PASS: Role changes are reflected and identity corrections invalidate the old JWT identity.");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Cookie", cookie);
    }
}
