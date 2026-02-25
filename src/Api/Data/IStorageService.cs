namespace Api.Data;

public interface IStorageService
{
    Task<string> UploadAsync(string key, string content);
    Task<string> DownloadAsync(string key);
}
