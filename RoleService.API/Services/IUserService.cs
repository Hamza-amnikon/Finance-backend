using RoleService.API.DTOs;

namespace RoleService.API.Services
{
    public enum UserChangeResult
    {
        Success,
        NotFound,
        DuplicateIdentity,
        ConcurrencyConflict
    }

    public interface IUserService
    {
        Task<List<UserResponse>> GetAllAsync(
            CancellationToken cancellationToken);

        Task<UserResponse?> GetByIdAsync(
            Guid userId,
            CancellationToken cancellationToken);

        Task<(UserChangeResult Result, UserResponse? User)> CreateAsync(
            CreateUserRequest request,
            CancellationToken cancellationToken);

        Task<(UserChangeResult Result, UserResponse? User)> UpdateAsync(
            Guid userId,
            UpdateUserRequest request,
            CancellationToken cancellationToken);

        Task<(UserChangeResult Result, UserResponse? User)> UpdateMicrosoftIdentityAsync(
            Guid userId,
            UpdateMicrosoftIdentityRequest request,
            CancellationToken cancellationToken);
    }
}
