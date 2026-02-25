using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Api.Auth;

public class SupabaseAuthHandler : AuthenticationHandler<SupabaseAuthOptions>
{
    private readonly HttpClient _httpClient;

    public SupabaseAuthHandler(
        IOptionsMonitor<SupabaseAuthOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        HttpClient httpClient)
        : base(options, logger, encoder)
    {
        _httpClient = httpClient;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return AuthenticateResult.NoResult();

        var authHeader = Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{Options.SupabaseUrl}/auth/v1/user");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("apikey", Options.SupabaseAnonKey);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return AuthenticateResult.Fail("Invalid or expired token");

            var json = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<SupabaseUser>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (user == null || string.IsNullOrEmpty(user.Id))
                return AuthenticateResult.Fail("Could not extract user from Supabase response");

            var claims = new List<Claim>
            {
                new("sub", user.Id),
                new(ClaimTypes.NameIdentifier, user.Id),
                new("role", user.Role ?? "authenticated")
            };

            if (!string.IsNullOrEmpty(user.Email))
                claims.Add(new Claim(ClaimTypes.Email, user.Email));

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            return AuthenticateResult.Fail($"Authentication failed: {ex.Message}");
        }
    }
}

public class SupabaseAuthOptions : AuthenticationSchemeOptions
{
    public string SupabaseUrl { get; set; } = string.Empty;
    public string SupabaseAnonKey { get; set; } = string.Empty;
}

public class SupabaseUser
{
    public string Id { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Role { get; set; }
}

public static class SupabaseAuth
{
    public const string SchemeName = "Supabase";

    public static IServiceCollection AddSupabaseAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpClient<SupabaseAuthHandler>();

        services.AddAuthentication(SchemeName)
            .AddScheme<SupabaseAuthOptions, SupabaseAuthHandler>(SchemeName, options =>
            {
                options.SupabaseUrl = config["SUPABASE_URL"]
                    ?? config.GetSection("Supabase")["Url"]
                    ?? "";
                options.SupabaseAnonKey = config["SUPABASE_ANON_KEY"]
                    ?? config.GetSection("Supabase")["AnonKey"]
                    ?? "";
            });

        services.AddAuthorization();
        return services;
    }

    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub")
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Missing user id in token");
        return Guid.Parse(sub);
    }
}
