using System.ComponentModel.DataAnnotations;
using PermissionService.API.CallOtherApis;
using PermissionService.API.DTOs;
using PermissionService.API.Models;
using PermissionService.API.Repositories;

namespace PermissionService.API.Services
{
    public class RolePermissionService : IRolePermissionService
    {
        private readonly IRolePermissionRepository _repository;
        private readonly IRoleServiceClient _roleServiceClient;

        public RolePermissionService(
            IRolePermissionRepository repository,
            IRoleServiceClient roleServiceClient)
        {
            _repository = repository;
            _roleServiceClient = roleServiceClient;
        }

        public async Task<List<RolePermissionResponse>> GetByRoleIdAsync(
            int roleId,
            CancellationToken cancellationToken)
        {
            await GetRequiredRoleAsync(
                roleId,
                cancellationToken);

            var assignments = await _repository.GetByRoleIdAsync(
                roleId,
                cancellationToken);

            return MapToResponses(assignments);
        }

        public async Task<List<RolePermissionResponse>> SaveAsync(
            SaveRolePermissionsRequest request,
            CancellationToken cancellationToken)
        {
            ValidateRequest(request);

            var role = await GetRequiredRoleAsync(
                request.RoleId,
                cancellationToken);

            if (!string.Equals(
                role.Status,
                PermissionStatus.Active,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException(
                    "Permissions can only be assigned to an active role.");
            }

            var permissionIds = new List<int>();
            var uniquePermissionIds = new HashSet<int>();

            foreach (var permissionRequest in request.Permissions)
            {
                ValidatePermissionRequest(permissionRequest);

                if (!uniquePermissionIds.Add(
                    permissionRequest.PermissionId))
                {
                    throw new ValidationException(
                        "The same permission cannot be assigned more than once.");
                }

                permissionIds.Add(
                    permissionRequest.PermissionId);
            }

            var permissions = await _repository.GetPermissionsByIdsAsync(
                permissionIds,
                cancellationToken);

            var permissionsById = permissions.ToDictionary(
                permission => permission.PermissionId);

            var assignments = new List<RolePermission>();

            foreach (var permissionRequest in request.Permissions)
            {
                if (!permissionsById.TryGetValue(
                    permissionRequest.PermissionId,
                    out var permission))
                {
                    throw new ValidationException(
                        $"Permission {permissionRequest.PermissionId} was not found.");
                }

                if (permission.Status != PermissionStatus.Active)
                {
                    throw new ValidationException(
                        $"Permission '{permission.PermissionCode}' is inactive.");
                }

                var assignment = MapToAssignment(
                    request.RoleId,
                    permissionRequest);

                assignments.Add(assignment);
            }

            await _repository.SaveAsync(
                request.RoleId,
                assignments,
                cancellationToken);

            var savedAssignments = await _repository.GetByRoleIdAsync(
                request.RoleId,
                cancellationToken);

            return MapToResponses(savedAssignments);
        }

        private async Task<RoleLookupResponse> GetRequiredRoleAsync(
            int roleId,
            CancellationToken cancellationToken)
        {
            if (roleId <= 0)
            {
                throw new ValidationException(
                    "Select a valid role.");
            }

            var role = await _roleServiceClient.GetByIdAsync(
                roleId,
                cancellationToken);

            if (role == null)
            {
                throw new ValidationException(
                    "The selected role was not found.");
            }

            return role;
        }

        private static void ValidateRequest(
            SaveRolePermissionsRequest request)
        {
            if (request == null)
            {
                throw new ValidationException(
                    "Role permission details are required.");
            }

            var validationContext = new ValidationContext(request);

            Validator.ValidateObject(
                request,
                validationContext,
                validateAllProperties: true);
        }

        private static void ValidatePermissionRequest(
            RolePermissionRequest request)
        {
            if (request == null)
            {
                throw new ValidationException(
                    "Permission assignments cannot contain empty entries.");
            }

            var validationContext = new ValidationContext(request);

            Validator.ValidateObject(
                request,
                validationContext,
                validateAllProperties: true);

            var hasOtherActions =
                request.CanCreate ||
                request.CanEdit ||
                request.CanDelete ||
                request.CanApprove ||
                request.CanExport ||
                request.CanIssue ||
                request.CanPost ||
                request.CanVoid ||
                request.CanReverse ||
                request.CanReconcile;

            if (hasOtherActions && !request.CanView)
            {
                throw new ValidationException(
                    "Enable View before granting other actions for a permission.");
            }
        }

        private static RolePermission MapToAssignment(
            int roleId,
            RolePermissionRequest request)
        {
            return new RolePermission
            {
                RoleId = roleId,
                PermissionId = request.PermissionId,
                CanView = request.CanView,
                CanCreate = request.CanCreate,
                CanEdit = request.CanEdit,
                CanDelete = request.CanDelete,
                CanApprove = request.CanApprove,
                CanExport = request.CanExport,
                CanIssue = request.CanIssue,
                CanPost = request.CanPost,
                CanVoid = request.CanVoid,
                CanReverse = request.CanReverse,
                CanReconcile = request.CanReconcile
            };
        }

        private static List<RolePermissionResponse> MapToResponses(
            List<RolePermission> assignments)
        {
            var responses = new List<RolePermissionResponse>();

            foreach (var assignment in assignments)
            {
                var response = new RolePermissionResponse
                {
                    RolePermissionId = assignment.RolePermissionId,
                    RoleId = assignment.RoleId,
                    PermissionId = assignment.PermissionId,
                    CanView = assignment.CanView,
                    CanCreate = assignment.CanCreate,
                    CanEdit = assignment.CanEdit,
                    CanDelete = assignment.CanDelete,
                    CanApprove = assignment.CanApprove,
                    CanExport = assignment.CanExport,
                    CanIssue = assignment.CanIssue,
                    CanPost = assignment.CanPost,
                    CanVoid = assignment.CanVoid,
                    CanReverse = assignment.CanReverse,
                    CanReconcile = assignment.CanReconcile,
                    CreatedDate = assignment.CreatedDate,
                    ModifiedDate = assignment.ModifiedDate,
                    RowVersion = assignment.RowVersion
                };

                responses.Add(response);
            }

            return responses;
        }
    }
}