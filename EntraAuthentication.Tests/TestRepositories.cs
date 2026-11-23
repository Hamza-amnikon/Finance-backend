using RoleService.API.Models;
using RoleService.API.Repositories;

sealed class TestUserRepository(User user) : IUserRepository
{
    public User User { get; } = user;

    public bool Exists { get; set; } = true;

    public Func<CancellationToken, Task<User?>>? LookupOverride { get; set; }

    public Task<User?> GetByMicrosoftIdentityAsync(
        Guid tenantId,
        Guid objectId,
        CancellationToken cancellationToken)
    {
        if (LookupOverride != null)
        {
            return LookupOverride(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Exists && tenantId == User.TenantId && objectId == User.ObjectId
            ? User
            : null);
    }

    public Task<List<User>> GetAllAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public void Add(User user)
    {
        throw new NotSupportedException();
    }

    public void SetOriginalRowVersion(User user, byte[] rowVersion)
    {
        throw new NotSupportedException();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}

sealed class TestRoleRepository : IRoleRepository
{
    public string RoleName { get; set; } = "Test CA";
    public bool Active { get; set; } = true;

    public Func<CancellationToken, Task<Roles?>>? LookupOverride { get; set; }

    public Task<Roles?> GetByIdAsync(int roleId, CancellationToken cancellationToken)
    {
        if (LookupOverride != null)
        {
            return LookupOverride(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Roles?>(new Roles
        {
            RoleId = roleId,
            Role = RoleName,
            Status = Active ? RoleStatus.Active : RoleStatus.Inactive
        });
    }

    public Task<List<Roles>> GetAllAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<List<Roles>> GetActiveAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public void Add(Roles role)
    {
        throw new NotSupportedException();
    }

    public void Remove(Roles role)
    {
        throw new NotSupportedException();
    }

    public void SetOriginalRowVersion(Roles role, byte[] rowVersion)
    {
        throw new NotSupportedException();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}
