namespace BeRdp.Backend.Services;

public interface IActiveDirectoryConfigurationService
{
    Task<ActiveDirectoryConfiguration> GetAsync(CancellationToken cancellationToken);
    Task<ActiveDirectoryConfiguration> SaveAsync(ActiveDirectoryConfiguration configuration, CancellationToken cancellationToken);
}
