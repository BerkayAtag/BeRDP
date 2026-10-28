namespace BeRdp.Backend.Services;

public interface IActiveDirectoryService
{
    Task<ActiveDirectoryDirectoryUser> GetUserBySidAsync(
        string sid,
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken);

    Task TestConnectionAsync(
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken);

    Task<bool> ValidateCredentialsAsync(
        string username,
        string password,
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ActiveDirectoryDirectoryUser>> SearchUsersAsync(
        string query,
        int limit,
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ActiveDirectoryDirectoryGroup>> SearchGroupsAsync(
        string query,
        int limit,
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetUserGroupNamesAsync(
        string username,
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken);
}
