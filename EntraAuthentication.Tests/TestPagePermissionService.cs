using RoleService.API.DTOs;
using RoleService.API.Services;

public sealed class TestPagePermissionService : IUserPagePermissionService
{
    public int Calls { get; private set; }

    public int LastRoleId { get; private set; }

    public Task<List<UserPagePermissionResponse>> GetAsync(
        int roleId,
        CancellationToken cancellationToken)
    {
        Calls++;
        LastRoleId = roleId;

        return Task.FromResult(new List<UserPagePermissionResponse>
        {
            new UserPagePermissionResponse
            {
                PermissionCode = "accounting.general-ledger",
                Module = "Accounting",
                Route = "/accounting/general-ledger",
                CompanyId = 2,
                CanView = true
            }
        });
    }
}
