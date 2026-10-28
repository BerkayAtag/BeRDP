namespace BeRdp.Backend.Services;

public interface IRdpAvailabilityService
{
    Task<bool> IsAvailableAsync(string ipAddress, int port, CancellationToken cancellationToken);
}
