using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RoleService.API.DTOs;
using RoleService.API.Models;
using RoleService.API.Repositories;
using RoleService.API.Services;

var roles = new TestRoleRepository();
var users = new TestUserRepository();
var service = new UserService(users, roles);
var request = Request();
var created = await service.CreateAsync(request, default);

Check(created.Result == UserChangeResult.Success && created.User != null,
    "A user can be created with an existing active role.");
Check(created.User!.Name == "Test user" && created.User.Role == "Existing active role",
    "The name is trimmed and the role name comes from the existing role.");
Check(!created.User.IsActive && created.User.RowVersion.Length == 8,
    "No automatic activation is added and the saved row version is returned.");

var duplicate = await service.CreateAsync(request, default);
Check(duplicate.Result == UserChangeResult.DuplicateIdentity && users.SaveCalls == 1,
    "An existing Microsoft identity is rejected even when the user is inactive.");

var otherTenant = Request();
otherTenant.ObjectId = request.ObjectId;
var otherTenantResult = await service.CreateAsync(otherTenant, default);
Check(otherTenantResult.Result == UserChangeResult.Success,
    "The same object ID in a different tenant is a separate identity.");

foreach (var errorNumber in new[] { 2601, 2627 })
{
    var concurrentUsers = new TestUserRepository
    {
        SaveFailure = new DbUpdateException(
            "A concurrent insert used the same identity.",
            CreateSqlException(errorNumber))
    };
    var concurrentResult = await new UserService(concurrentUsers, roles)
        .CreateAsync(Request(), default);

    Check(concurrentResult.Result == UserChangeResult.DuplicateIdentity && concurrentResult.User == null,
        $"SQL duplicate error {errorNumber} is handled when concurrent creation bypasses the initial check.");
}

var removedRoleUsers = new TestUserRepository
{
    SaveFailure = new DbUpdateException(
        "The selected role was removed during save.",
        CreateSqlException(547))
};
await RejectValidation(() => new UserService(removedRoleUsers, roles)
    .CreateAsync(Request(), default));
Check(true, "A role removed during user creation returns a validation error.");

foreach (var roleId in new[] { 0, 8, 999 })
{
    var invalid = Request();
    invalid.RoleId = roleId;
    await RejectValidation(() => service.CreateAsync(invalid, default));
}
Check(users.SaveCalls == 2,
    "Invalid, inactive and missing roles cannot create users.");

var missingTenant = Request();
missingTenant.TenantId = Guid.Empty;
await RejectValidation(() => service.CreateAsync(missingTenant, default));
var missingObject = Request();
missingObject.ObjectId = Guid.Empty;
await RejectValidation(() => service.CreateAsync(missingObject, default));
var invalidEmail = Request();
invalidEmail.Email = "invalid-email";
await RejectValidation(() => service.CreateAsync(invalidEmail, default));
var missingName = Request();
missingName.Name = "   ";
await RejectValidation(() => service.CreateAsync(missingName, default));
await RejectValidation(() => service.CreateAsync(null!, default));
Check(users.SaveCalls == 2,
    "Invalid Microsoft identity fields, names and emails never reach save.");

var existing = users.Users[0];
var originalTenant = existing.TenantId;
var originalObject = existing.ObjectId;
var originalCreated = existing.CreatedDate;
var update = Update(existing);
update.Name = " Updated name ";
update.IsActive = true;
var updated = await service.UpdateAsync(existing.UserId, update, default);
Check(updated.Result == UserChangeResult.Success && updated.User!.Name == "Updated name",
    "A valid update succeeds and trims editable fields.");
Check(existing.TenantId == originalTenant && existing.ObjectId == originalObject &&
    existing.CreatedDate == originalCreated && existing.ModifiedDate.HasValue,
    "Updating a user preserves Microsoft identity and original creation date.");

var stale = Update(existing);
stale.RowVersion = new byte[8];
var conflict = await service.UpdateAsync(existing.UserId, stale, default);
Check(conflict.Result == UserChangeResult.ConcurrencyConflict && conflict.User == null,
    "A stale row version returns a concurrency conflict.");

var invalidVersion = Update(existing);
invalidVersion.RowVersion = [];
await RejectValidation(() => service.UpdateAsync(existing.UserId, invalidVersion, default));
var inactiveRole = Update(existing);
inactiveRole.RoleId = 8;
await RejectValidation(() => service.UpdateAsync(existing.UserId, inactiveRole, default));
Check(true, "Missing row versions and assigning inactive roles are rejected.");

var userWithInactiveRole = new User
{
    TenantId = Guid.NewGuid(),
    ObjectId = Guid.NewGuid(),
    Name = "Inactive-role user",
    Email = "inactive@example.test",
    RoleId = 8,
    IsActive = true,
    RowVersion = new byte[8]
};
users.Users.Add(userWithInactiveRole);
var disable = Update(userWithInactiveRole);
disable.IsActive = false;
var disabled = await service.UpdateAsync(userWithInactiveRole.UserId, disable, default);
Check(disabled.Result == UserChangeResult.Success && !disabled.User!.IsActive,
    "An administrator can disable a user even when the existing role is inactive.");
var enable = Update(userWithInactiveRole);
enable.IsActive = true;
await RejectValidation(() => service.UpdateAsync(userWithInactiveRole.UserId, enable, default));
Check(true, "An inactive role cannot be used to enable a user.");

Check((await service.GetByIdAsync(userWithInactiveRole.UserId, default))?.Role == "Inactive role",
    "Inactive users and roles remain visible for administration.");
Check((await service.GetAllAsync(default)).Count == 3,
    "Listing users returns their existing role names.");
Check(await service.GetByIdAsync(Guid.NewGuid(), default) == null,
    "A missing user can be reported as not found.");
Check((await service.UpdateAsync(Guid.NewGuid(), Update(existing), default)).Result == UserChangeResult.NotFound,
    "Updating a missing user returns NotFound.");
await RejectValidation(() => service.GetByIdAsync(Guid.Empty, default));

var identityUsers = new TestUserRepository();
var identityService = new UserService(identityUsers, roles);
await identityService.CreateAsync(Request(), default);
var identityUser = identityUsers.Users.Single();
var originalIdentity = (
    identityUser.UserId,
    identityUser.TenantId,
    identityUser.Name,
    identityUser.Email,
    identityUser.RoleId,
    identityUser.IsActive,
    identityUser.CreatedDate);
var oldObjectId = identityUser.ObjectId;
var originalVersion = identityUser.RowVersion.ToArray();
var identityRequest = new UpdateMicrosoftIdentityRequest
{
    ObjectId = Guid.NewGuid(),
    RowVersion = originalVersion.ToArray()
};
var corrected = await identityService.UpdateMicrosoftIdentityAsync(
    identityUser.UserId,
    identityRequest,
    default);
Check(corrected.Result == UserChangeResult.Success &&
    corrected.User!.ObjectId == identityRequest.ObjectId &&
    (identityUser.UserId, identityUser.TenantId, identityUser.Name, identityUser.Email,
        identityUser.RoleId, identityUser.IsActive, identityUser.CreatedDate) == originalIdentity,
    "Correcting an identity preserves the user, tenant, role, activation, profile and creation date.");
Check(await identityUsers.GetByMicrosoftIdentityAsync(identityUser.TenantId, oldObjectId, default) == null &&
    await identityUsers.GetByMicrosoftIdentityAsync(identityUser.TenantId, identityRequest.ObjectId, default) == identityUser,
    "Only the corrected Microsoft identity now resolves to the existing user.");

var staleIdentity = await identityService.UpdateMicrosoftIdentityAsync(
    identityUser.UserId,
    new UpdateMicrosoftIdentityRequest
    {
        ObjectId = Guid.NewGuid(),
        RowVersion = originalVersion
    },
    default);
Check(staleIdentity.Result == UserChangeResult.ConcurrencyConflict &&
    identityUser.ObjectId == identityRequest.ObjectId,
    "A stale identity correction is rejected before the user is mutated.");

var otherIdentityRequest = Request();
otherIdentityRequest.TenantId = identityUser.TenantId;
await identityService.CreateAsync(otherIdentityRequest, default);
var duplicateIdentity = await identityService.UpdateMicrosoftIdentityAsync(
    identityUser.UserId,
    new UpdateMicrosoftIdentityRequest
    {
        ObjectId = otherIdentityRequest.ObjectId,
        RowVersion = identityUser.RowVersion.ToArray()
    },
    default);
Check(duplicateIdentity.Result == UserChangeResult.DuplicateIdentity &&
    identityUser.ObjectId == identityRequest.ObjectId,
    "Another user's Microsoft identity cannot be taken over by a correction.");

identityRequest.RowVersion = identityUser.RowVersion.ToArray();
Check((await identityService.UpdateMicrosoftIdentityAsync(identityUser.UserId, identityRequest, default))
    .Result == UserChangeResult.Success,
    "Keeping the same Microsoft identity does not trigger a false duplicate conflict.");
identityRequest.RowVersion = identityUser.RowVersion.ToArray();
Check((await identityService.UpdateMicrosoftIdentityAsync(Guid.NewGuid(), identityRequest, default))
    .Result == UserChangeResult.NotFound,
    "An identity correction cannot recreate a missing user.");
await RejectValidation(() => identityService.UpdateMicrosoftIdentityAsync(
    Guid.Empty, identityRequest, default));
await RejectValidation(() => identityService.UpdateMicrosoftIdentityAsync(
    identityUser.UserId, null!, default));
await RejectValidation(() => identityService.UpdateMicrosoftIdentityAsync(
    identityUser.UserId,
    new UpdateMicrosoftIdentityRequest
    {
        ObjectId = Guid.Empty,
        RowVersion = identityUser.RowVersion.ToArray()
    },
    default));
await RejectValidation(() => identityService.UpdateMicrosoftIdentityAsync(
    identityUser.UserId,
    new UpdateMicrosoftIdentityRequest
    {
        ObjectId = Guid.NewGuid(),
        RowVersion = []
    },
    default));
Check(true, "Invalid identity corrections are rejected without changing the user.");

foreach (var saveFailure in new Exception[]
{
    new DbUpdateConcurrencyException("Concurrent identity update."),
    new DbUpdateException("Duplicate identity.", CreateSqlException(2601)),
    new DbUpdateException("Duplicate identity.", CreateSqlException(2627))
})
{
    var failedUsers = new TestUserRepository { SaveFailure = saveFailure };
    var failedUser = new User
    {
        TenantId = Guid.NewGuid(),
        ObjectId = Guid.NewGuid(),
        Name = "Existing user",
        Email = "existing@example.test",
        RoleId = 7,
        RowVersion = new byte[8]
    };
    failedUsers.Users.Add(failedUser);
    var failed = await new UserService(failedUsers, roles).UpdateMicrosoftIdentityAsync(
        failedUser.UserId,
        new UpdateMicrosoftIdentityRequest
        {
            ObjectId = Guid.NewGuid(),
            RowVersion = failedUser.RowVersion.ToArray()
        },
        default);
    Check(failed.Result == (saveFailure is DbUpdateConcurrencyException
        ? UserChangeResult.ConcurrencyConflict
        : UserChangeResult.DuplicateIdentity),
        "Concurrent identity changes and database duplicate constraints produce controlled conflicts.");
}

using var cancellation = new CancellationTokenSource();
cancellation.Cancel();

try
{
    await service.GetAllAsync(cancellation.Token);
    throw new Exception("Cancellation was ignored.");
}
catch (OperationCanceledException)
{
    Check(true, "Cancellation is propagated.");
}

Console.WriteLine("User service checks passed. No database connections were opened.");

static CreateUserRequest Request()
{
    return new CreateUserRequest
    {
        TenantId = Guid.NewGuid(),
        ObjectId = Guid.NewGuid(),
        Name = " Test user ",
        Email = "user@example.test",
        RoleId = 7
    };
}

static UpdateUserRequest Update(User user)
{
    return new UpdateUserRequest
    {
        Name = user.Name,
        Email = user.Email,
        RoleId = user.RoleId,
        IsActive = user.IsActive,
        RowVersion = user.RowVersion.ToArray()
    };
}

static async Task RejectValidation(Func<Task> action)
{
    try
    {
        await action();
    }
    catch (ValidationException)
    {
        return;
    }

    throw new Exception("Invalid user input was accepted.");
}

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception(message);
    }

    Console.WriteLine("PASS: " + message);
}

static SqlException CreateSqlException(int number)
{
    // Construct the provider error in memory; these tests never connect to SQL Server.
    var constructor = typeof(SqlError)
        .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
        .Where(candidate => candidate.GetParameters()[0].ParameterType == typeof(int))
        .OrderBy(candidate => candidate.GetParameters().Length)
        .First();
    var arguments = constructor.GetParameters()
        .Select(parameter => parameter.ParameterType.IsValueType
            ? Activator.CreateInstance(parameter.ParameterType)
            : parameter.ParameterType == typeof(string) ? "Test" : null)
        .ToArray();
    arguments[0] = number;

    var error = (SqlError)constructor.Invoke(arguments);
    var errors = (SqlErrorCollection)Activator.CreateInstance(
        typeof(SqlErrorCollection),
        nonPublic: true)!;
    typeof(SqlErrorCollection)
        .GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!
        .Invoke(errors, [error]);

    var factory = typeof(SqlException).GetMethod(
        "CreateException",
        BindingFlags.NonPublic | BindingFlags.Static,
        binder: null,
        types: [typeof(SqlErrorCollection), typeof(string)],
        modifiers: null)!;

    return (SqlException)factory.Invoke(null, [errors, "Test"])!;
}

sealed class TestUserRepository : IUserRepository
{
    private byte[]? _originalRowVersion;
    private User? _updatingUser;

    public List<User> Users { get; } = [];

    public Exception? SaveFailure { get; init; }

    public int SaveCalls { get; private set; }

    public Task<List<User>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Users.ToList());
    }

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Users.SingleOrDefault(user => user.UserId == userId));
    }

    public Task<User?> GetByMicrosoftIdentityAsync(
        Guid tenantId,
        Guid objectId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Users.SingleOrDefault(user =>
            user.TenantId == tenantId && user.ObjectId == objectId));
    }

    public void Add(User user)
    {
        Users.Add(user);
    }

    public void SetOriginalRowVersion(User user, byte[] rowVersion)
    {
        _originalRowVersion = rowVersion;
        _updatingUser = user;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (SaveFailure != null)
        {
            return Task.FromException(SaveFailure);
        }

        if (_originalRowVersion != null &&
            !_updatingUser!.RowVersion.SequenceEqual(_originalRowVersion))
        {
            _originalRowVersion = null;
            throw new DbUpdateConcurrencyException("The user changed.");
        }

        SaveCalls++;
        _originalRowVersion = null;

        foreach (var user in Users)
        {
            user.RowVersion = BitConverter.GetBytes((long)SaveCalls);
        }

        return Task.CompletedTask;
    }
}

sealed class TestRoleRepository : IRoleRepository
{
    public Task<List<Roles>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new List<Roles>
        {
            new Roles { RoleId = 7, Role = "Existing active role", Status = RoleStatus.Active },
            new Roles { RoleId = 8, Role = "Inactive role", Status = RoleStatus.Inactive }
        });
    }

    public async Task<Roles?> GetByIdAsync(int roleId, CancellationToken cancellationToken)
    {
        return (await GetAllAsync(cancellationToken)).SingleOrDefault(role => role.RoleId == roleId);
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
