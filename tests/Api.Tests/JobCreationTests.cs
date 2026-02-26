using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Dtos;

namespace Api.Tests;

public class JobCreationTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public JobCreationTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient();
        var token = TestWebAppFactory.TokenForUser(TestWebAppFactory.TestUserId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task CreateJob_ValidRequest_Returns201()
    {
        var request = new CreateJobRequest(
            "My WPF App",
            "BlazorServer",
            new List<FileInput>
            {
                new("MainWindow.xaml", "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Grid></Grid></Window>"),
                new("MainWindow.xaml.cs", "using System.Windows; namespace MyApp { public partial class MainWindow : Window { } }")
            }
        );

        var response = await _client.PostAsJsonAsync("/api/jobs", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var job = await response.Content.ReadFromJsonAsync<JobResponse>();
        Assert.NotNull(job);
        Assert.Equal("My WPF App", job.Name);
        Assert.Equal("BlazorServer", job.Target);
        Assert.Equal("created", job.Status);
    }

    [Fact]
    public async Task CreateJob_EmptyFiles_Returns400()
    {
        var request = new CreateJobRequest("Test", "BlazorServer", new List<FileInput>());
        var response = await _client.PostAsJsonAsync("/api/jobs", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateJob_InvalidExtension_Returns400()
    {
        var request = new CreateJobRequest(
            "Test",
            "BlazorServer",
            new List<FileInput> { new("virus.exe", "content") }
        );

        var response = await _client.PostAsJsonAsync("/api/jobs", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateJob_InvalidTarget_Returns400()
    {
        var request = new CreateJobRequest(
            "Test",
            "InvalidTarget",
            new List<FileInput> { new("MainWindow.xaml", "<Window/>") }
        );

        var response = await _client.PostAsJsonAsync("/api/jobs", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateJob_PathTraversal_Returns400()
    {
        var request = new CreateJobRequest(
            "Test",
            "BlazorServer",
            new List<FileInput> { new("../../etc/passwd.xaml", "<Window/>") }
        );

        var response = await _client.PostAsJsonAsync("/api/jobs", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListJobs_ReturnsOnlyOwnJobs()
    {
        var response = await _client.GetAsync("/api/jobs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jobs = await response.Content.ReadFromJsonAsync<List<JobListItem>>();
        Assert.NotNull(jobs);
    }

    [Fact]
    public async Task GetJob_ExistingJob_ReturnsDetails()
    {
        var createRequest = new CreateJobRequest(
            "Detail Test",
            "BlazorWasm",
            new List<FileInput> { new("App.xaml", "<Application/>") }
        );

        var createResponse = await _client.PostAsJsonAsync("/api/jobs", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<JobResponse>();

        var response = await _client.GetAsync($"/api/jobs/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var job = await response.Content.ReadFromJsonAsync<JobResponse>();
        Assert.Equal("Detail Test", job!.Name);
        Assert.Equal("BlazorWasm", job.Target);
    }

    [Fact]
    public async Task GetJob_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/jobs/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
