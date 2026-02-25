using System.Security.Claims;
using System.Text.Encodings.Web;
using Api.Ai;
using Api.Auth;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.Tests;

public class TestWebAppFactory : WebApplicationFactory<Program>
{
    public static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("SUPABASE_URL", "https://test.supabase.co");
        builder.UseSetting("SUPABASE_ANON_KEY", "test");
        builder.UseSetting("SUPABASE_SERVICE_ROLE_KEY", "test");
        builder.UseSetting("AI_PROVIDER", "openai");
        builder.UseSetting("OPENAI_API_KEY", "test-key");
        builder.UseSetting("OPENAI_MODEL", "gpt-4o");
        builder.UseSetting("ENABLE_SWAGGER", "false");
        builder.UseSetting("DATABASE_URL", "Host=localhost;Database=test;Username=test;Password=test");

        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(IJobRepository) ||
                d.ServiceType == typeof(IAiClient)
            ).ToList();
            foreach (var d in descriptors) services.Remove(d);

            services.AddScoped<IJobRepository, InMemoryJobRepository>();
            services.AddSingleton<IAiClient>(new FakeAiClient());

            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "Test";
                o.DefaultChallengeScheme = "Test";
                o.SchemeMap.Remove(SupabaseAuth.SchemeName);
            });

            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });
    }

    public static string TokenForUser(Guid userId) => $"test-token-{userId}";
}

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return Task.FromResult(AuthenticateResult.NoResult());

        var authHeader = Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer test-token-"))
            return Task.FromResult(AuthenticateResult.Fail("Invalid test token"));

        var userId = authHeader.Replace("Bearer test-token-", "");

        var claims = new[]
        {
            new Claim("sub", userId),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("role", "authenticated")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class FakeAiClient : IAiClient
{
    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        if (systemPrompt.Contains("migration architect"))
        {
            return Task.FromResult(@"{
                ""detected_patterns"": [""MVVM""],
                ""key_views"": [""MainWindow.xaml""],
                ""key_viewmodels"": [""MainViewModel.cs""],
                ""control_inventory"": {""Button"": 5},
                ""estimated_risk"": ""low"",
                ""recommended_approach"": [""Phase 1: Convert views""],
                ""suggested_work_breakdown"": [{""phase"": ""Phase 1"", ""description"": ""Convert"", ""effort_days"": 3}]
            }");
        }

        return Task.FromResult("FILE: Pages/Index.razor\n<h1>Hello</h1>\nEND_FILE");
    }
}

public class InMemoryJobRepository : IJobRepository
{
    private static readonly List<Job> Jobs = new();
    private static readonly List<JobFile> Files = new();
    private static readonly List<JobLog> Logs = new();
    private static readonly object Lock = new();

    public Task<Job> CreateJobAsync(Job job)
    {
        lock (Lock)
        {
            job.Id = Guid.NewGuid();
            job.CreatedAt = DateTime.UtcNow;
            job.UpdatedAt = DateTime.UtcNow;
            Jobs.Add(job);
            return Task.FromResult(job);
        }
    }

    public Task<Job?> GetJobAsync(Guid jobId)
    {
        lock (Lock)
        {
            return Task.FromResult(Jobs.FirstOrDefault(j => j.Id == jobId));
        }
    }

    public Task<List<Job>> GetJobsByUserAsync(Guid userId)
    {
        lock (Lock)
        {
            return Task.FromResult(Jobs.Where(j => j.UserId == userId).OrderByDescending(j => j.CreatedAt).ToList());
        }
    }

    public Task UpdateJobAsync(Job job)
    {
        lock (Lock)
        {
            job.UpdatedAt = DateTime.UtcNow;
            return Task.CompletedTask;
        }
    }

    public Task<bool> TryLockJobForProcessingAsync(Guid jobId, string fromStatus, string toStatus)
    {
        lock (Lock)
        {
            var job = Jobs.FirstOrDefault(j => j.Id == jobId && j.Status == fromStatus);
            if (job == null) return Task.FromResult(false);
            job.Status = toStatus;
            return Task.FromResult(true);
        }
    }

    public Task CreateJobFileAsync(JobFile file)
    {
        lock (Lock)
        {
            file.Id = Guid.NewGuid();
            file.CreatedAt = DateTime.UtcNow;
            Files.Add(file);
            return Task.CompletedTask;
        }
    }

    public Task<List<JobFile>> GetJobFilesByKindAsync(Guid jobId, string kind)
    {
        lock (Lock)
        {
            return Task.FromResult(Files.Where(f => f.JobId == jobId && f.Kind == kind).ToList());
        }
    }

    public Task<List<JobFile>> GetAllJobFilesAsync(Guid jobId)
    {
        lock (Lock)
        {
            return Task.FromResult(Files.Where(f => f.JobId == jobId).ToList());
        }
    }

    public Task AddLogAsync(Guid jobId, string level, string message)
    {
        lock (Lock)
        {
            Logs.Add(new JobLog
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                Ts = DateTime.UtcNow,
                Level = level,
                Message = message
            });
            return Task.CompletedTask;
        }
    }

    public Task<List<JobLog>> GetLogsAsync(Guid jobId)
    {
        lock (Lock)
        {
            return Task.FromResult(Logs.Where(l => l.JobId == jobId).OrderBy(l => l.Ts).ToList());
        }
    }
}
