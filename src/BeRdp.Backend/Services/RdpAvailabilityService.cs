using System.Net.Sockets;
using Microsoft.Extensions.Caching.Memory;

namespace BeRdp.Backend.Services;

public sealed class RdpAvailabilityService(IMemoryCache cache, ILogger<RdpAvailabilityService> logger)
    : IRdpAvailabilityService, IDisposable
{
    private readonly SemaphoreSlim _concurrency = new(32);

    public async Task<bool> IsAvailableAsync(string ipAddress, int port, CancellationToken cancellationToken)
    {
        var key = (ipAddress, port);
        if (cache.TryGetValue(key, out bool cached))
        {
            return cached;
        }

        await _concurrency.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(750));
            using var connection = new TcpClient();
            var available = false;
            try
            {
                await connection.ConnectAsync(ipAddress, port, timeout.Token);
                available = true;
            }
            catch (SocketException ex)
            {
                logger.LogDebug("RDP probe {Address}:{Port} failed: {Error}.", ipAddress, port, ex.SocketErrorCode);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug("RDP probe {Address}:{Port} timed out.", ipAddress, port);
            }

            cache.Set(key, available, TimeSpan.FromSeconds(20));
            return available;
        }
        finally
        {
            _concurrency.Release();
        }
    }

    public void Dispose() => _concurrency.Dispose();
}
