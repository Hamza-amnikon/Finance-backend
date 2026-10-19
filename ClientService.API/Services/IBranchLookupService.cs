namespace ClientService.API.Services
{
    public interface IBranchLookupService
    {
        Task<string?> GetActiveNameAsync(
            string name,
            CancellationToken cancellationToken);
    }
}
