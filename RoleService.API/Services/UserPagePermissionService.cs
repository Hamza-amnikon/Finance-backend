using RoleService.API.DTOs;
using RoleService.API.Repositories;

namespace RoleService.API.Services
{
    public sealed class UserPagePermissionService : IUserPagePermissionService
    {
        private readonly IUserPagePermissionRepository _repository;

        public UserPagePermissionService(IUserPagePermissionRepository repository)
        {
            _repository = repository;
        }

        public Task<List<UserPagePermissionResponse>> GetAsync(
            int roleId,
            CancellationToken cancellationToken)
        {
            if (roleId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(roleId));
            }

            return _repository.GetByRoleIdAsync(roleId, cancellationToken);
        }
    }
}
