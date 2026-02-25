namespace Api;

public class SupabaseSettings
{
    public string Url { get; set; } = string.Empty;
    public string AnonKey { get; set; } = string.Empty;
    public string ServiceRoleKey { get; set; } = string.Empty;
}

public class AiSettings
{
    public string Provider { get; set; } = "openai";
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string OpenAiModel { get; set; } = "gpt-4o";
    public string AzureEndpoint { get; set; } = string.Empty;
    public string AzureKey { get; set; } = string.Empty;
    public string AzureDeployment { get; set; } = string.Empty;
}

public class StorageSettings
{
    public bool UseSupabaseStorage { get; set; }
    public string Bucket { get; set; } = "wpf-blazor-files";
}

public class AppSettings
{
    public string FrontendOrigin { get; set; } = "http://localhost:5173";
    public int MaxFileBytes { get; set; } = 1_048_576;
    public int MaxFilesPerJob { get; set; } = 50;
    public bool EnableSwagger { get; set; }
}
