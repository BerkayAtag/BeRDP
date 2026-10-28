using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeRdp.Client.Models;

namespace BeRdp.Client.Services;

public sealed class ApiException(HttpStatusCode statusCode, string message) : InvalidOperationException(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class ApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _httpClient;
    public Uri BaseAddress => _httpClient.BaseAddress!;

    public ApiClient(string baseUrl)
    {
        _httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = BackendAddressStore.Validate(baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<LoginResponse> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("login", new { username, password }, cancellationToken);
        var result = await ReadResponse<LoginResponse>(response, cancellationToken);
        if (result.UserId == Guid.Empty || string.IsNullOrEmpty(result.Token))
        {
            throw new InvalidOperationException("Sunucu geçerli bir oturum döndürmedi.");
        }

        return result;
    }

    public async Task<LoginResponse> LoginWithWindowsAsync(CancellationToken cancellationToken = default)
    {
        using var handler = new HttpClientHandler
        {
            UseDefaultCredentials = true,
            AllowAutoRedirect = false
        };
        using var windowsClient = new HttpClient(handler)
        {
            BaseAddress = BaseAddress,
            Timeout = TimeSpan.FromSeconds(30)
        };
        using var response = await windowsClient.PostAsync("login/windows", null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ApiException(response.StatusCode,
                "Windows SSO doğrulanamadı. Domain oturumu, sunucu DNS/SPN ve Windows Authentication ayarlarını kontrol edin.");
        }

        var result = await ReadResponse<LoginResponse>(response, cancellationToken);
        if (result.UserId == Guid.Empty || string.IsNullOrEmpty(result.Token) || !result.IsActiveDirectoryUser)
        {
            throw new InvalidOperationException("Sunucu geçerli bir AD SSO oturumu döndürmedi.");
        }
        return result;
    }

    public async Task<IReadOnlyList<MachineDto>> GetMachinesAsync(string token, CancellationToken cancellationToken = default)
    {
        const int pageSize = 500;
        var machines = new Dictionary<Guid, MachineDto>();
        for (var skip = 0; ; skip += pageSize)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"machines?assignedOnly=true&skip={skip}&take={pageSize}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var page = await ReadResponse<List<MachineDto>>(response, cancellationToken);
            foreach (var machine in page)
            {
                machines[machine.Id] = machine;
            }

            if (page.Count < pageSize)
            {
                return machines.Values.ToList();
            }
        }
    }

    public async Task<MachineDto> GetAssignedMachineAsync(Guid id, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"machines/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadResponse<MachineDto>(response, cancellationToken);
    }

    private static async Task<T> ReadResponse<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(response.StatusCode, ReadErrorMessage(payload, response.StatusCode));
        }

        try
        {
            return JsonSerializer.Deserialize<T>(payload, JsonOptions) ??
                throw new InvalidOperationException("Sunucu boş bir JSON cevabı döndürdü.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Sunucu cevabı geçerli JSON biçiminde değil.", ex);
        }
    }

    private static string ReadErrorMessage(string payload, HttpStatusCode statusCode)
    {
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in new[] { "message", "detail", "title" })
                    {
                        if (document.RootElement.TryGetProperty(field, out var node) &&
                            node.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(node.GetString()))
                        {
                            return node.GetString()!;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                return BuildStatusMessage(statusCode, "Sunucu logunu kontrol edin.");
            }
        }

        return BuildStatusMessage(statusCode, null);
    }

    private static string BuildStatusMessage(HttpStatusCode statusCode, string? suffix)
    {
        var hint = statusCode switch
        {
            HttpStatusCode.MethodNotAllowed =>
                "Girdiğiniz adres yönetim konsolu (portal) olabilir. Masaüstü istemcisi için API adresini kullanın (ör. http://sunucu:5001).",
            HttpStatusCode.NotFound =>
                "API uç noktası bulunamadı. Sunucu adresini kontrol edin (ör. http://sunucu:5001).",
            _ => null
        };

        var message = hint is null
            ? $"İstek başarısız: HTTP {(int)statusCode}."
            : $"İstek başarısız: HTTP {(int)statusCode}. {hint}";

        return suffix is null ? message : $"{message} {suffix}";
    }

    public void Dispose() => _httpClient.Dispose();
}
