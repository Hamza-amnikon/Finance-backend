namespace AzureADService.DTOs
{
    public record DirectoryUserResponse(
        Guid TenantId,
        Guid ObjectId,
        string? DisplayName,
        string? Email,
        string? UserPrincipalName);
}
