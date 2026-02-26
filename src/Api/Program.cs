using Api;
using Api.Ai;
using Api.Auth;
using Api.Data;
using Api.Endpoints;
using Api.Middleware;
using Api.Validation;
using Api.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

var supabaseSettings = new SupabaseSettings
{
    Url = builder.Configuration["SUPABASE_URL"] ?? builder.Configuration.GetSection("Supabase")["Url"] ?? "",
    AnonKey = builder.Configuration["SUPABASE_ANON_KEY"] ?? builder.Configuration.GetSection("Supabase")["AnonKey"] ?? "",
    ServiceRoleKey = builder.Configuration["SUPABASE_SERVICE_ROLE_KEY"] ?? builder.Configuration.GetSection("Supabase")["ServiceRoleKey"] ?? ""
};

var aiSettings = new AiSettings
{
    Provider = builder.Configuration["AI_PROVIDER"] ?? builder.Configuration.GetSection("Ai")["Provider"] ?? "openai",
    OpenAiApiKey = builder.Configuration["OPENAI_API_KEY"] ?? builder.Configuration.GetSection("Ai")["OpenAiApiKey"] ?? "",
    OpenAiModel = builder.Configuration["OPENAI_MODEL"] ?? builder.Configuration.GetSection("Ai")["OpenAiModel"] ?? "gpt-4o",
    AzureEndpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"] ?? builder.Configuration.GetSection("Ai")["AzureEndpoint"] ?? "",
    AzureKey = builder.Configuration["AZURE_OPENAI_KEY"] ?? builder.Configuration.GetSection("Ai")["AzureKey"] ?? "",
    AzureDeployment = builder.Configuration["AZURE_OPENAI_DEPLOYMENT"] ?? builder.Configuration.GetSection("Ai")["AzureDeployment"] ?? ""
};

var storageSettings = new StorageSettings
{
    UseSupabaseStorage = bool.TryParse(builder.Configuration["USE_SUPABASE_STORAGE"], out var useStorage) && useStorage,
    Bucket = builder.Configuration["SUPABASE_STORAGE_BUCKET"] ?? builder.Configuration.GetSection("Storage")["Bucket"] ?? "wpf-blazor-files"
};

var appSettings = new AppSettings
{
    FrontendOrigin = builder.Configuration["FRONTEND_ORIGIN"] ?? builder.Configuration.GetSection("App")["FrontendOrigin"] ?? "http://localhost:5173",
    MaxFileBytes = int.TryParse(builder.Configuration["MAX_FILE_BYTES"], out var maxBytes) ? maxBytes : 1_048_576,
    MaxFilesPerJob = int.TryParse(builder.Configuration["MAX_FILES_PER_JOB"], out var maxFiles) ? maxFiles : 50,
    EnableSwagger = bool.TryParse(builder.Configuration["ENABLE_SWAGGER"], out var swagger) && swagger
};

builder.Services.AddSingleton(supabaseSettings);
builder.Services.AddSingleton(aiSettings);
builder.Services.AddSingleton(storageSettings);
builder.Services.AddSingleton(appSettings);

builder.Services.AddSingleton<IJobRepository>(
    new SupabaseRestRepository(supabaseSettings.Url, supabaseSettings.ServiceRoleKey));

builder.Services.AddSingleton<FileValidator>(_ => new FileValidator(appSettings.MaxFileBytes, appSettings.MaxFilesPerJob));

if (storageSettings.UseSupabaseStorage)
    builder.Services.AddSingleton<IStorageService>(
        new SupabaseStorageService(supabaseSettings.Url, supabaseSettings.ServiceRoleKey, storageSettings.Bucket));
else
    builder.Services.AddSingleton<IStorageService>(new NoOpStorageService());

if (aiSettings.Provider.Equals("azure", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IAiClient>(
        new AzureOpenAiClient(aiSettings.AzureEndpoint, aiSettings.AzureKey, aiSettings.AzureDeployment));
else
    builder.Services.AddSingleton<IAiClient>(
        new OpenAiClient(aiSettings.OpenAiApiKey, aiSettings.OpenAiModel));

builder.Services.AddSingleton<JobQueue>();
builder.Services.AddHostedService<JobWorker>();

builder.Services.AddSupabaseAuth(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

if (appSettings.EnableSwagger)
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

builder.Logging.AddJsonConsole();

var app = builder.Build();

app.UseCors();
app.UseMiddleware<ProblemDetailsMiddleware>();

if (appSettings.EnableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/docs", () => Results.Redirect("/docs.html")).ExcludeFromDescription();
app.MapHealthEndpoints();
app.MapJobEndpoints();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");

public partial class Program { }
