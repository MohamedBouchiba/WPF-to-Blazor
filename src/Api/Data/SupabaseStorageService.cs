using System.Net.Http.Headers;
using System.Text;

namespace Api.Data;

public class SupabaseStorageService : IStorageService
{
    private readonly HttpClient _httpClient;
    private readonly string _bucket;

    public SupabaseStorageService(string supabaseUrl, string serviceRoleKey, string bucket)
    {
        _bucket = bucket;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"{supabaseUrl.TrimEnd('/')}/storage/v1/")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);
        _httpClient.DefaultRequestHeaders.Add("apikey", serviceRoleKey);
    }

    public async Task<string> UploadAsync(string key, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var httpContent = new ByteArrayContent(bytes);
        httpContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        var response = await _httpClient.PostAsync($"object/{_bucket}/{key}", httpContent);
        response.EnsureSuccessStatusCode();
        return key;
    }

    public async Task<string> DownloadAsync(string key)
    {
        var response = await _httpClient.GetAsync($"object/{_bucket}/{key}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}

public class NoOpStorageService : IStorageService
{
    public Task<string> UploadAsync(string key, string content) =>
        Task.FromResult(key);

    public Task<string> DownloadAsync(string key) =>
        Task.FromResult(string.Empty);
}
